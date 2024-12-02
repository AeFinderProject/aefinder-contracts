using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AeFinder.Contracts
{
    // Contract class must inherit the base class generated from the proto file
    public class BillingContract : BillingContractContainer.BillingContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(!State.Initialized.Value, "Already Initialized");
            State.TokenContract.Value = Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            Assert(State.TokenContract.Value != null, "Cannot find token contract!");
            
            State.Initialized.Value = true;
            State.Admin.Value = input.Admin;
            State.Treasurer.Value = input.Treasurer;
            State.FeeAddress.Value = input.FeeAddress;
            State.FeeSymbols.Value = input.Symbols;
            return new Empty();
        }

        public override Empty SetAdmin(Address input)
        {
            Assert(Context.Sender == State.Admin.Value, "No Permission");
            State.Admin.Value = input;
            return new Empty();
        }

        public override Empty SetTreasurer(Address input)
        {
            Assert(Context.Sender == State.Admin.Value, "No Permission");
            State.Treasurer.Value = input;
            return new Empty();
        }

        public override Empty SetFeeAddress(Address input)
        {
            Assert(Context.Sender == State.Admin.Value, "No Permission");
            State.FeeAddress.Value = input;
            return new Empty();
        }

        public override Empty SetFeeSymbol(SymbolList input)
        {
            Assert(Context.Sender == State.Admin.Value, "No Permission");
            State.FeeSymbols.Value = input;
            Context.Fire(new FeeSymbolSet
            {
                Symbols = input
            });
            return new Empty();
        }

        public override Empty Deposit(DepositInput input)
        {
            Assert(State.FeeSymbols.Value.Value.Contains(input.Symbol), "Invalid symbol");
            Assert( input.Amount > 0, "Invalid amount");
            
            var organizationAddress = State.UserOrganizationMap[Context.Sender];
            if (organizationAddress == null)
            {
                var organizationHash = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(Context.Sender),
                    Context.TransactionId);
                organizationAddress = Context.ConvertVirtualAddressToContractAddress(organizationHash);
                State.UserOrganizationMap[Context.Sender] = organizationAddress;
                var member = new OrganizationMember
                {
                    Address = Context.Sender,
                    Role = OrganizationMemberRole.Admin
                };
                State.Organizations[organizationAddress] = new Organization
                {
                    Address = organizationAddress,
                    Members = { member }
                };
                Context.Fire(new OrganizationCreated
                {
                    Address = organizationAddress,
                    Members = new OrganizationMemberList
                    {
                        Value = { member }
                    }
                });
            }
            
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = Context.Sender,
                Symbol = input.Symbol,
                Amount = input.Amount,
                To = Context.Self
            });
            
            State.Balances[organizationAddress][input.Symbol] = State.Balances[organizationAddress][input.Symbol].Add(input.Amount);
            
            Context.Fire(new Deposited
            {
                Address = organizationAddress,
                Symbol = input.Symbol,
                Amount = input.Amount
            });
            return new Empty();
        }

        public override Empty Withdraw(WithdrawInput input)
        {
            Assert( input.Amount > 0, "Invalid amount");
            
            var organization = State.UserOrganizationMap[Context.Sender];
            Assert(organization != null, "No Organization");
            
            var balance = State.Balances[organization][input.Symbol];

            Assert(balance >= input.Amount, "Insufficient balance");
            
            State.TokenContract.Transfer.Send(new TransferInput
            {
                Symbol = input.Symbol,
                Amount = input.Amount,
                To = input.Address
            });
            State.Balances[organization][input.Symbol] = balance.Sub(input.Amount);
            
            Context.Fire(new Withdrawn
            {
                Address = organization,
                Symbol = input.Symbol,
                Amount = input.Amount,
                ToAddress = input.Address
            });
            return new Empty();
        }

        public override Empty LockFrom(LockFromInput input)
        {
            Assert(Context.Sender == State.Treasurer.Value, "No Permission");
            var organization = State.Organizations[input.Adderss];
            Assert(organization != null, "No Organization");
            LockFrom(input.Adderss,input.Symbol,input.Amount);
            return new Empty();
        }
        
        public override Empty Lock(LockInput input)
        {
            var organization = State.UserOrganizationMap[Context.Sender];
            Assert(organization != null, "No Organization");
            Assert(!string.IsNullOrWhiteSpace(input.OrderId), "Invalid order id");
            LockFrom(organization,input.Symbol,input.Amount, input.OrderId);
            return new Empty();
        }

        public override Empty Charge(ChargeInput input)
        {
            Assert(Context.Sender == State.Treasurer.Value, "No Permission");
            Assert(input.ChargeAmount >= 0 , "Invalid charge amount");
            Assert(input.UnlockAmount >= 0 , "Invalid unlock amount");
            Assert(input.ChargeAmount != 0 || input.UnlockAmount != 0, "ChargeAmount and UnlockAmount cannot both be 0");

            var lockBalance = State.LockedBalances[input.Adderss][input.Symbol];
            Assert(lockBalance >= input.ChargeAmount.Add(input.UnlockAmount), "Insufficient locked balance");
            State.LockedBalances[input.Adderss][input.Symbol] = lockBalance.Sub(input.ChargeAmount).Sub(input.UnlockAmount);
            State.Balances[input.Adderss][input.Symbol] = State.Balances[input.Adderss][input.Symbol].Add(input.UnlockAmount);

            if (input.ChargeAmount > 0)
            {
                State.TokenContract.Transfer.Send(new TransferInput
                {
                    To = State.FeeAddress.Value,
                    Symbol = input.Symbol,
                    Amount = input.ChargeAmount
                });
                Context.Fire(new FeeReceived
                {
                    FeeAddress = State.FeeAddress.Value,
                    Symbol = input.Symbol,
                    Amount = input.ChargeAmount,
                    UserAdderss = input.Adderss
                });
            }
            
            Context.Fire(new Charged
            {
                Adderss = input.Adderss,
                Symbol = input.Symbol,
                ChargedAmount = input.ChargeAmount,
                UnlockedAmount = input.UnlockAmount
            });
            return new Empty();
        }

        public override Address GetAdmin(Empty input)
        {
            return State.Admin.Value;
        }

        public override Address GetTreasurer(Empty input)
        {
            return State.Treasurer.Value;
        }

        public override Address GetFeeAddress(Empty input)
        {
            return State.FeeAddress.Value;
        }

        public override SymbolList GetFeeSymbols(Empty input)
        {
            return State.FeeSymbols.Value;
        }

        public override GetBalanceOutput GetBalance(GetBalanceInput input)
        {
            return new GetBalanceOutput
            {
                Balance = State.Balances[input.Address][input.Symbol],
                LockedBalance = State.LockedBalances[input.Address][input.Symbol]
            };
        }
        
        private void LockFrom(Address address, string symbol, long amount, string orderId = "")
        {
            Assert( amount > 0, "Invalid amount");
            
            var balance = State.Balances[address][symbol];
            Assert(balance >= amount, "Insufficient balance");
            State.Balances[address][symbol]= balance.Sub(amount);
            State.LockedBalances[address][symbol] = State.LockedBalances[address][symbol].Add(amount);
            
            Context.Fire(new Locked
            {
                Address = address,
                Amount = amount,
                Symbol = symbol,
                OrderId = orderId
            });
        }
    }
    
}