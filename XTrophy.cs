namespace Expload {

    using Pravda;
    using System;
    using Standards;

    [Program]
    public class XTrophy {
        public static void Main() { }

        private Mapping<Bytes, Int64> Balance =
            new Mapping<Bytes, Int64>();

        private Mapping<Bytes, WithdrawalData> FrozenBalance =
            new Mapping<Bytes, WithdrawalData>();

        private Mapping<Bytes, sbyte> WhiteList =
            new Mapping<Bytes, sbyte>();

        // Accrue amount of XTrophy to recipient.
        public void Accrue(Bytes recipient, Int64 amount) {
            increaseBalance(recipient, amount);
            Log.Event("Accrue", new EventData(recipient, amount));
        }

        // Refund frozen amount of XTrophy to recipient.
        public void Refund(Bytes recipient, Int64 amount) {
            increaseBalance(recipient, amount);
            Log.Event("Refund", new EventData(recipient, amount));
        }

        // Remove amount of XTrophy from balance of address.
        public void Burn(Bytes address, Int64 amount) {
            assertIsOwner();
            Require(amount > 0, "Amount must be positive");
            Int64 balance = Balance.GetOrDefault(address, 0);
            if (balance >= amount) {
                Balance[address] = balance - amount;
                Log.Event("Burn", new EventData(address, amount));
            } else {
                Error.Throw("XTrophyError: Not enough funds for Burn");
            }
        }

        // Add address to white list
        public void WhiteListAdd(Bytes address) {
            assertIsOwner();
            WhiteList[address] = 1;
        }

        // Remove address from white list.
        public void WhiteListRemove(Bytes address) {
            assertIsOwner();
            WhiteList[address] = 0;
        }

        public Int64 MyBalance()
        {
            Bytes sender = Info.Sender();
            Int64 senderBalance = Balance.GetOrDefault(sender, 0);
            return senderBalance;
        }

        // Send XTrophy from transaction Sender to recipient.
        // Recipient should be present in the white list.
        public void Spend(Bytes recipient, Int64 amount) {
            if (WhiteListCheck(recipient)) {
                Require(amount > 0, "Amount must be positive");
                Bytes sender = Info.Sender();
                Int64 senderBalance = Balance.GetOrDefault(sender, 0);
                Int64 recipientBalance = Balance.GetOrDefault(recipient, 0);
                if (senderBalance >= amount) {
                    Balance[sender] = senderBalance - amount;
                    Balance[recipient] = recipientBalance + amount;
                    Log.Event("Spend", new SpendEventData(sender, recipient, amount));
                } else {
                    Error.Throw("XTrophyError: Not enough funds for Spend operation");
                }
            } else Error.Throw("XTrophyError: Operation denied");
        }

        // Send XTrophys from recipient to sender.
        // Sender should be present in the white list.
        // Can only be called from program present in the white list or by such program.
        public void Give(Bytes sender, Bytes recipient, Int64 amount) {
            if (WhiteListCheck(sender) && (Info.Sender() == sender || IsCalledFrom(sender))) {
                Require(amount > 0, "Amount must be positive");
                Int64 senderBalance = Balance.GetOrDefault(sender, 0);
                Int64 recipientBalance = Balance.GetOrDefault(recipient, 0);
                if (senderBalance >= amount) {
                    Balance[sender] = senderBalance - amount;
                    Balance[recipient] = recipientBalance + amount;
                    Log.Event("Give", new GiveEventData(sender, recipient, amount));
                } else {
                    Error.Throw("XTrophyError: Not enough funds for Give operation");
                }
            } else Error.Throw("XTrophyError: Operation denied");
        }

        private void increaseBalance(Bytes recipient, Int64 amount) {
           assertIsOwner();
           Require(amount > 0, "Amount must be positive");

           Int64 lastBalance = Balance.GetOrDefault(recipient, 0);
           Int64 newBalance = lastBalance + amount;
           Balance[recipient] = newBalance;
        }


        //// Withdrawal methods

        // Freeze XTrophy.
        public void WithdrawalRequest(Int64 amount, String cardNumberHash)
        {
            Require(amount > 0, "Amount must be positive");

            Bytes sender = Info.Sender();
            Require(!FrozenBalance.ContainsKey(sender), "Withdrawal is already there");

            Int64 balance = Balance.GetOrDefault(sender, 0);
            Require(balance >= amount, "Not enough XTrophy for Withdrawal");

            Balance[sender] = balance - amount;
            FrozenBalance[sender] = new WithdrawalData(amount, cardNumberHash);

            Log.Event("WithdrawalRequest", new EventData(sender, amount));
            Log.Event("Burn", new EventData(sender, amount));
        }

        // Burn frozen XTrophy.
        public void WithdrawalComplete(Bytes address)
        {
            assertIsOwner();

            nonEmptyFrozenBalance(address);

            Int64 frozenBalance = FrozenBalance[address].amount;
            FrozenBalance.Remove(address);

            Log.Event("WithdrawalComplete", new EventData(address, frozenBalance));
        }

        // Defrosting XTrophy.
        public void WithdrawalCancel(Bytes address)
        {
            assertIsOwner();

            nonEmptyFrozenBalance(address);

            Int64 balance = Balance.GetOrDefault(address, 0);
            Int64 frozenBalance = FrozenBalance[address].amount;
            Balance[address] = balance + frozenBalance;
            FrozenBalance.Remove(address);

            Log.Event("WithdrawalCancel", new EventData(address, frozenBalance));
            Log.Event("Refund", new EventData(address, frozenBalance));
        }

        // Get withdrawal data.
        public WithdrawalData GetWithdrawalData(Bytes address)
        {
            nonEmptyFrozenBalance(address);

            WithdrawalData withdrawalData = FrozenBalance[address];

            return withdrawalData;
        }


        //// Private methods

        // Check address is white listed/
        private bool WhiteListCheck(Bytes address)
        {
            return WhiteList.GetOrDefault(address, 0) == 1;
        }

        private void assertIsOwner()
        {
            if (Info.Sender() != Info.ProgramAddress())
            {
                Error.Throw("XTrophyError: Only owner of the program can do that.");
            }
        }

        private void nonEmptyFrozenBalance(Bytes address)
        {
            Require(FrozenBalance.ContainsKey(address), "No frozen funds");
        }

        // Check if XTrophy program was called from another program
        private bool IsCalledFrom(Bytes address) {
            if(Info.Callers().Length < 2) return false;
            if(Info.Callers()[Info.Callers().Length-2] != address) return false;
            return true;
        }

        private void Require(Boolean condition, String message)
        {
            if (!condition)
            {
                Error.Throw("XTrophyError: " + message);
            }
        }
    }

    class EventData {
        public EventData(Bytes recipient, Int64 amount) {
            this.recipient = recipient;
            this.amount = amount;
        }
        public Int64 amount;
        public Bytes recipient;
    }

    class SpendEventData {
        public SpendEventData(Bytes sender, Bytes recipient, Int64 amount) {
            this.sender = sender;
            this.recipient = recipient;
            this.amount = amount;
        }
        public Bytes sender;
        public Bytes recipient;
        public Int64 amount;
    }

    class GiveEventData {
        public GiveEventData(Bytes sender, Bytes recipient, Int64 amount) {
            this.sender = sender;
            this.recipient = recipient;
            this.amount = amount;
        }
        public Bytes sender;
        public Bytes recipient;
        public Int64 amount;
    }
}

namespace Expload.Standards
{
    using System;

    public class WithdrawalData
    {
        public WithdrawalData(Int64 amount, String cardNumberHash)
        {
            this.amount = amount;
            this.cardNumberHash = cardNumberHash;
        }

        public Int64 amount;
        public String cardNumberHash;
    }
}
