namespace Expload {

    using Pravda;
    using System;

    [Program]
    public class XTrophy {
        public static void Main() { }

        private Mapping<Bytes, Int64> Balance =
            new Mapping<Bytes, Int64>();

        private Mapping<Bytes, sbyte> WhiteList =
            new Mapping<Bytes, sbyte>();

        // Gives amount of XTrophy to recipient.
        public void Give(Bytes recipient, Int64 amount) {
            assertIsOwner();
            Require(amount > 0, "Amount must be positive");

            Int64 lastBalance = Balance.GetOrDefault(recipient, 0);
            Int64 newBalance = lastBalance + amount;
            Balance[recipient] = newBalance;
            Log.Event("Give", new EventData(recipient, amount));
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
                    Log.Event("Spend", new EventData(recipient, amount));
                } else {
                    Error.Throw("XTrophyError: Not enough funds for Spend operation");
                }
            } else Error.Throw("XTrophyError: Operation denied");
        }

        // Send XTrophys from recipient to sender.
        // Sender should be present in the white list.
        // Can only be called from program present in the white list or by such program.
        public void Refund(Bytes sender, Bytes recipient, Int64 amount) {
            if (WhiteListCheck(sender) && (Info.Sender() == sender || IsCalledFrom(sender))) {
                Require(amount > 0, "Amount must be positive");
                Int64 senderBalance = Balance.GetOrDefault(sender, 0);
                Int64 recipientBalance = Balance.GetOrDefault(recipient, 0);
                if (senderBalance >= amount) {
                    Balance[sender] = senderBalance - amount;
                    Balance[recipient] = recipientBalance + amount;
                    Log.Event("Refund", new EventData(recipient, amount));
                } else {
                    Error.Throw("XTrophyError: Not enough funds for Refund operation");
                }
            } else Error.Throw("XTrophyError: Operation denied");
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
}
