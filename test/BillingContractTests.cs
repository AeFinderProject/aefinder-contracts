using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace AeFinder.Contracts;

public class BillingContractTests : TestBase
{
    [Fact]
    public async Task InitializeTest()
    {
        
        await InitializeContractAsync();
            
        var admin = await AdminBillingContractStub.GetAdmin.CallAsync(new Empty());
        admin.ShouldBe(AdminAddress);
            
        var treasurer = await AdminBillingContractStub.GetTreasurer.CallAsync(new Empty());
        treasurer.ShouldBe(TreasurerAddress);
            
        var receiveFeeAddress = await AdminBillingContractStub.GetFeeAddress.CallAsync(new Empty());
        receiveFeeAddress.ShouldBe(FeeAddress);
            
        var symbolList = await AdminBillingContractStub.GetFeeSymbols.CallAsync(new Empty());
        symbolList.ShouldBe(new SymbolList
        {
            Value = { Symbols }
        });
            
        var result = await AdminBillingContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = AdminAddress,
            Treasurer = TreasurerAddress,
            FeeAddress = FeeAddress,
            Symbols = new SymbolList
            {
                Value =  { Symbols}
            }
        });
        result.TransactionResult.Error.ShouldContain("Already Initialized");
    }

    [Fact]
    public async Task SetAdminTest()
    {
        await InitializeContractAsync();
        await AdminBillingContractStub.SetAdmin.SendAsync(NewAdminAddress);
        
        var admin = await AdminBillingContractStub.GetAdmin.CallAsync(new Empty());
        admin.ShouldBe(NewAdminAddress);
        
        var result = await AdminBillingContractStub.SetAdmin.SendWithExceptionAsync(NewAdminAddress);
        result.TransactionResult.Error.ShouldContain("No Permission");
    }
    
    [Fact]
    public async Task SetTreasurerTest()
    {
        await InitializeContractAsync();
        await AdminBillingContractStub.SetTreasurer.SendAsync(NewTreasurerAddress);
        
        var treasurer = await AdminBillingContractStub.GetTreasurer.CallAsync(new Empty());
        treasurer.ShouldBe(NewTreasurerAddress);
        
        var result = await OtherAdminBillingContractStub.SetAdmin.SendWithExceptionAsync(NewTreasurerAddress);
        result.TransactionResult.Error.ShouldContain("No Permission");
    }
    
    [Fact]
    public async Task SetFeeAddressTest()
    {
        await InitializeContractAsync();
        await AdminBillingContractStub.SetFeeAddress.SendAsync(NewFeeAddress);
        
        var treasurer = await AdminBillingContractStub.GetFeeAddress.CallAsync(new Empty());
        treasurer.ShouldBe(NewFeeAddress);
        
        var result = await OtherAdminBillingContractStub.SetAdmin.SendWithExceptionAsync(NewFeeAddress);
        result.TransactionResult.Error.ShouldContain("No Permission");
    }
    
    [Fact]
    public async Task SetFeeSymbolTest()
    {
        await InitializeContractAsync();
        var symbols = new SymbolList
        {
            Value = { "USDT", "ELF", "ETH" },
        };
        var result = await AdminBillingContractStub.SetFeeSymbol.SendAsync(symbols);
        var logEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == "FeeSymbolSet");
        var feeSymbolSet = new FeeSymbolSet();
        feeSymbolSet.MergeFrom(logEvent);
        feeSymbolSet.Symbols.ShouldBe(symbols);
        
        var symbolList = await AdminBillingContractStub.GetFeeSymbols.CallAsync(new Empty());
        symbolList.ShouldBe(symbols);
        
        result = await OtherAdminBillingContractStub.SetFeeSymbol.SendWithExceptionAsync(symbols);
        result.TransactionResult.Error.ShouldContain("No Permission");
    }
    
    [Fact]
    public async Task DepositTest()
    {
        await InitializeContractAsync();

        var symbol = "ELF";
        var amount = 100_00000000;
        var transactionResult = await DepositAsync(symbol, amount);

        var organizationCreated = GetOrganizationCreated(transactionResult);
        var organizationAddress = organizationCreated.Address;
        organizationAddress.ShouldNotBeNull();
        organizationCreated.Members.Value.Count.ShouldBe(1);
        organizationCreated.Members.Value[0].Address.ShouldBe(UserAddress);
        organizationCreated.Members.Value[0].Role.ShouldBe(OrganizationMemberRole.Admin);
        
        var depositedLogEvent = transactionResult.Logs.FirstOrDefault(l => l.Name == "Deposited");
        var deposited = new Deposited();
        deposited.MergeFrom(depositedLogEvent);
        deposited.Address.ShouldBe(organizationAddress);
        deposited.Symbol.ShouldBe(symbol);
        deposited.Amount.ShouldBe(amount);

        var contractBalance = await UserTokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        contractBalance.Symbol.ShouldBe(symbol);
        contractBalance.Balance.ShouldBe(amount);

        var organizationBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        organizationBalance.Balance.ShouldBe(amount);
        organizationBalance.LockedBalance.ShouldBe(0);
        
        transactionResult = await DepositAsync(symbol, amount);
        GetOrganizationCreated(transactionResult).ShouldBeNull();
        
        depositedLogEvent = transactionResult.Logs.FirstOrDefault(l => l.Name == "Deposited");
        deposited = new Deposited();
        deposited.MergeFrom(depositedLogEvent);
        deposited.Address.ShouldBe(organizationAddress);
        deposited.Symbol.ShouldBe(symbol);
        deposited.Amount.ShouldBe(amount);
        
        contractBalance = await UserTokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        contractBalance.Symbol.ShouldBe(symbol);
        contractBalance.Balance.ShouldBe(amount * 2);
        
        organizationBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        organizationBalance.Balance.ShouldBe(amount * 2);
        organizationBalance.LockedBalance.ShouldBe(0);
    }
    
    [Fact]
    public async Task DepositWithExceptionTest()
    {
        await InitializeContractAsync();

        var symbol = "ELF";
        var amount = 100_00000000;
        await UserTokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Spender = ContractAddress,
            Symbol = symbol,
            Amount = amount
        });

        var invalidSymbol = "BTC";
        var result = await UserBillingContractStub.Deposit.SendWithExceptionAsync(new DepositInput
        {
            Symbol = invalidSymbol,
            Amount = amount
        });
        result.TransactionResult.Error.ShouldContain("Invalid symbol");
        
        result = await UserBillingContractStub.Deposit.SendWithExceptionAsync(new DepositInput
        {
            Symbol = symbol,
            Amount = 0
        });
        result.TransactionResult.Error.ShouldContain("Invalid amount");

        var invalidAmount = -amount;
        result = await UserBillingContractStub.Deposit.SendWithExceptionAsync(new DepositInput
        {
            Symbol = symbol,
            Amount = invalidAmount
        });
        result.TransactionResult.Error.ShouldContain("Invalid amount");
    }

    [Fact]
    public async Task WithdrawTest()
    {
        await InitializeContractAsync();

        var symbol = "ELF";
        var amount = 100_00000000;
        var transactionResult = await DepositAsync(symbol, amount);
        var organizationCreated = GetOrganizationCreated(transactionResult);
        var organizationAddress = organizationCreated.Address;

        var initContractBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });

        var initBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        
        var withdrawAmount = 90_00000000;
        var result = await UserBillingContractStub.Withdraw.SendAsync(new WithdrawInput
        {
            Symbol = symbol,
            Amount = withdrawAmount,
            Address = OtherUserAddress
        });
        
        var withdrawnLogEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == "Withdrawn");
        var withdrawn = new Withdrawn();
        withdrawn.MergeFrom(withdrawnLogEvent);
        withdrawn.Address.ShouldBe(organizationAddress);
        withdrawn.Symbol.ShouldBe(symbol);
        withdrawn.Amount.ShouldBe(withdrawAmount);
        withdrawn.ToAddress.ShouldBe(OtherUserAddress);
        
        var currentContractBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        currentContractBalance.Balance.ShouldBe(initContractBalance.Balance - withdrawAmount);
        
        var userBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = OtherUserAddress
        });
        userBalance.Balance.ShouldBe(withdrawAmount);
        
        var currentBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        currentBalance.Balance.ShouldBe(initBalance.Balance - withdrawAmount);
        currentBalance.LockedBalance.ShouldBe(0);
        
        await UserBillingContractStub.Withdraw.SendAsync(new WithdrawInput
        {
            Symbol = symbol,
            Amount = currentBalance.Balance,
            Address = OtherUserAddress
        });
        
        currentBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        currentBalance.Balance.ShouldBe(0);
        currentBalance.LockedBalance.ShouldBe(0);
    }
    
    [Fact]
    public async Task WithdrawWithExceptionTest()
    {
        await InitializeContractAsync();

        var symbol = "ELF";
        var amount = 100_00000000;
        await DepositAsync(symbol, amount);

        var result = await UserBillingContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
        {
            Symbol = symbol,
            Amount = 0,
            Address = UserAddress
        });
        
        result.TransactionResult.Error.ShouldContain("Invalid amount");

        result = await UserBillingContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
        {
            Symbol = symbol,
            Amount = -amount,
            Address = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid amount");

        var excessAmount = amount + 1_00000000;
        result = await UserBillingContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
        {
            Symbol = symbol,
            Amount = excessAmount,
            Address = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Insufficient balance");
        
        result = await AdminBillingContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
        {
            Symbol = symbol,
            Amount = amount,
            Address = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("No Organization");
    }

    [Fact]
    public async Task LockFromTest()
    {
        await InitializeContractAsync();
        var symbol = "ELF";
        var amount = 100_00000000;
        var transactionResult = await DepositAsync(symbol, amount);
        var organizationAddress = GetOrganizationCreated(transactionResult).Address;
        
        var initBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        initBalance.LockedBalance.ShouldBe(0);
        
        var initContractBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        
        var lockedAmount = 10_00000000;
        var result = await TreasurerBillingContractStub.LockFrom.SendAsync(new LockFromInput
        {
            Symbol = symbol,
            Amount = lockedAmount,
            Adderss = organizationAddress
        });
        var lockLogEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == "Locked");
        var locked = new Locked();
        locked.MergeFrom(lockLogEvent);
        locked.Address.ShouldBe(organizationAddress);
        locked.Symbol.ShouldBe(symbol);
        locked.Amount.ShouldBe(lockedAmount);
        locked.OrderId.ShouldBeEmpty();
        
        var currentBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });

        currentBalance.Balance.ShouldBe(initBalance.Balance - lockedAmount);
        currentBalance.LockedBalance.ShouldBe(lockedAmount);
        
        var currentContractBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        currentContractBalance.ShouldBe(initContractBalance);
        
        await TreasurerBillingContractStub.LockFrom.SendAsync(new LockFromInput
        {
            Symbol = symbol,
            Amount = currentBalance.Balance,
            Adderss = organizationAddress
        });
        
        currentBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });

        currentBalance.Balance.ShouldBe(0);
        currentBalance.LockedBalance.ShouldBe(amount);
    }

    [Fact]
    public async Task LockFromWithExceptionTest()
    {
        await InitializeContractAsync();

        var symbol = "ELF";
        var amount = 100_00000000;
        var transactionResult = await DepositAsync(symbol, amount);
        var organizationAddress = GetOrganizationCreated(transactionResult).Address;
        var lockedAmount = 10_00000000;
        var result = await AdminBillingContractStub.LockFrom.SendWithExceptionAsync(new LockFromInput
        {
            Symbol = symbol,
            Amount = lockedAmount,
            Adderss = organizationAddress
        });
        result.TransactionResult.Error.ShouldContain("No Permission");
        
        result = await TreasurerBillingContractStub.LockFrom.SendWithExceptionAsync(new LockFromInput
        {
            Symbol = symbol,
            Amount = lockedAmount,
            Adderss = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("No Organization");
        
        result = await TreasurerBillingContractStub.LockFrom.SendWithExceptionAsync(new LockFromInput
        {
            Symbol = symbol,
            Amount = 0,
            Adderss = organizationAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid amount");
        
        result = await TreasurerBillingContractStub.LockFrom.SendWithExceptionAsync(new LockFromInput
        {
            Symbol = symbol,
            Amount = -lockedAmount,
            Adderss = organizationAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid amount");
        
        result = await TreasurerBillingContractStub.LockFrom.SendWithExceptionAsync(new LockFromInput
        {
            Symbol = symbol,
            Amount = amount + lockedAmount,
            Adderss = organizationAddress
        });
        result.TransactionResult.Error.ShouldContain("Insufficient balance");
    }

    [Fact]
    public async Task LockTest()
    {
        await InitializeContractAsync();

        var symbol = "ELF";
        var amount = 100_00000000;
        var transactionResult = await DepositAsync(symbol, amount);
        var organizationAddress = GetOrganizationCreated(transactionResult).Address;
       
        var initBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        initBalance.LockedBalance.ShouldBe(0);
        
        var initContractBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        
        var lockedAmount = 10_00000000;
        var orderId = "1";
        
        var result = await UserBillingContractStub.Lock.SendAsync(new LockInput
        {
            Symbol = symbol,
            Amount = lockedAmount,
            OrderId = orderId
        });
        
        var lockLogEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == "Locked");
        var locked = new Locked();
        locked.MergeFrom(lockLogEvent);
        locked.Address.ShouldBe(organizationAddress);
        locked.Symbol.ShouldBe(symbol);
        locked.Amount.ShouldBe(lockedAmount);
        locked.OrderId.ShouldBe("1");
        
        var currentBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });

        currentBalance.Balance.ShouldBe(initBalance.Balance - lockedAmount);
        currentBalance.LockedBalance.ShouldBe(lockedAmount);
        
        var currentContractBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        currentContractBalance.ShouldBe(initContractBalance);

        var otherOrderId = "2";
        await UserBillingContractStub.Lock.SendAsync(new LockInput
        {
            Symbol = symbol,
            Amount = currentBalance.Balance,
            OrderId = otherOrderId
        });
        
        currentBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });

        currentBalance.Balance.ShouldBe(0);
        currentBalance.LockedBalance.ShouldBe(amount);
    }
    
    [Fact]
    public async Task LockWithExceptionTest()
    {
        await InitializeContractAsync();

        var symbol = "ELF";
        var amount = 100_00000000;
        await DepositAsync(symbol, amount);
        var lockedAmount = 10_00000000;
        var orderId = "1";
        var result = await AdminBillingContractStub.Lock.SendWithExceptionAsync(new LockInput
        {
            Symbol = symbol,
            Amount = lockedAmount,
            OrderId = orderId
        });
        result.TransactionResult.Error.ShouldContain("No Organization");
        
        result = await UserBillingContractStub.Lock.SendWithExceptionAsync(new LockInput
        {
            Symbol = symbol,
            Amount = lockedAmount
        });
        result.TransactionResult.Error.ShouldContain("Invalid order id");
        
        result = await UserBillingContractStub.Lock.SendWithExceptionAsync(new LockInput
        {
            Symbol = symbol,
            Amount = lockedAmount,
            OrderId = "  "
        });
        result.TransactionResult.Error.ShouldContain("Invalid order id");
        
        result = await UserBillingContractStub.Lock.SendWithExceptionAsync(new LockInput
        {
            Symbol = symbol,
            Amount = 0,
            OrderId = orderId
        });
        result.TransactionResult.Error.ShouldContain("Invalid amount");
        
        result = await UserBillingContractStub.Lock.SendWithExceptionAsync(new LockInput
        {
            Symbol = symbol,
            Amount = -lockedAmount,
            OrderId = orderId
        });
        result.TransactionResult.Error.ShouldContain("Invalid amount");
        
        result = await UserBillingContractStub.Lock.SendWithExceptionAsync(new LockInput
        {
            Symbol = symbol,
            Amount = amount + lockedAmount,
            OrderId = orderId
        });
        result.TransactionResult.Error.ShouldContain("Insufficient balance");
    }

    [Fact]
    public async Task ChargeTest()
    {
        await InitializeContractAsync();

        var symbol = "ELF";
        var amount = 100_00000000;
        var transactionResult = await DepositAsync(symbol, amount);
        var organizationAddress = GetOrganizationCreated(transactionResult).Address;
        
        var lockedAmount = 10_00000000;
        var orderId = "1";
        
        await UserBillingContractStub.Lock.SendAsync(new LockInput
        {
            Symbol = symbol,
            Amount = lockedAmount,
            OrderId = orderId
        });
        
        var initBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        
        var initContractBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        
        var chargedAmount = 3_00000000;
        var unlockedAmount = 2_00000000;
        var feeAmount = 0;
        var result = await TreasurerBillingContractStub.Charge.SendAsync(new ChargeInput
        {
            Adderss = organizationAddress,
            Symbol = symbol,
            ChargeAmount = chargedAmount,
            UnlockAmount = unlockedAmount
        });
        
        var chargedLogEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == "Charged");
        var charged = new Charged();
        charged.MergeFrom(chargedLogEvent);
        charged.Adderss.ShouldBe(organizationAddress);
        charged.Symbol.ShouldBe(symbol);
        charged.ChargedAmount.ShouldBe(chargedAmount);
        charged.UnlockedAmount.ShouldBe(unlockedAmount);
        
        var feeReceivedLogEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == "FeeReceived");
        var feeReceived = new FeeReceived();
        feeReceived.MergeFrom(feeReceivedLogEvent);
        feeReceived.FeeAddress.ShouldBe(FeeAddress);
        feeReceived.Amount.ShouldBe(chargedAmount);
        feeReceived.UserAdderss.ShouldBe(organizationAddress);
        feeReceived.Symbol.ShouldBe(symbol);
        
        var currentBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        currentBalance.LockedBalance.ShouldBe(initBalance.LockedBalance - chargedAmount - unlockedAmount);
        currentBalance.Balance.ShouldBe(initBalance.Balance + unlockedAmount);
        
        var currentContractBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        currentContractBalance.Balance.ShouldBe(initContractBalance.Balance - chargedAmount);

        feeAmount += chargedAmount;
        var feeAddressBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = FeeAddress
        });
        feeAddressBalance.Balance.ShouldBe(feeAmount);

        var previousContractBalance = currentContractBalance;
        var previousBalance = currentBalance;
        unlockedAmount = 3_00000000;
        result = await TreasurerBillingContractStub.Charge.SendAsync(new ChargeInput
        {
            Adderss = organizationAddress,
            Symbol = symbol,
            ChargeAmount = 0,
            UnlockAmount = unlockedAmount
        });
        feeReceivedLogEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == "FeeReceived");
        feeReceivedLogEvent.ShouldBeNull();
        chargedLogEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == "Charged");
        charged = new Charged();
        charged.MergeFrom(chargedLogEvent);
        charged.Adderss.ShouldBe(organizationAddress);
        charged.Symbol.ShouldBe(symbol);
        charged.ChargedAmount.ShouldBe(0);
        charged.UnlockedAmount.ShouldBe(unlockedAmount);
        
        currentContractBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        currentContractBalance.Balance.ShouldBe(previousContractBalance.Balance);
        
        currentBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        currentBalance.LockedBalance.ShouldBe(previousBalance.LockedBalance - unlockedAmount);
        currentBalance.Balance.ShouldBe(previousBalance.Balance + unlockedAmount);
        
        previousContractBalance = currentContractBalance;
        previousBalance = currentBalance;
        chargedAmount = 2_00000000;
        result = await TreasurerBillingContractStub.Charge.SendAsync(new ChargeInput
        {
            Adderss = organizationAddress,
            Symbol = symbol,
            ChargeAmount = chargedAmount,
            UnlockAmount = 0
        });
        chargedLogEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == "Charged");
        charged = new Charged();
        charged.MergeFrom(chargedLogEvent);
        charged.Adderss.ShouldBe(organizationAddress);
        charged.Symbol.ShouldBe(symbol);
        charged.ChargedAmount.ShouldBe(chargedAmount);
        charged.UnlockedAmount.ShouldBe(0);
        
        feeReceivedLogEvent = result.TransactionResult.Logs.FirstOrDefault(l => l.Name == "FeeReceived");
        feeReceived = new FeeReceived();
        feeReceived.MergeFrom(feeReceivedLogEvent);
        feeReceived.FeeAddress.ShouldBe(FeeAddress);
        feeReceived.Amount.ShouldBe(chargedAmount);
        feeReceived.UserAdderss.ShouldBe(organizationAddress);
        feeReceived.Symbol.ShouldBe(symbol);
        
        currentContractBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = ContractAddress
        });
        currentContractBalance.Balance.ShouldBe(previousContractBalance.Balance - chargedAmount);
        
        currentBalance = await UserBillingContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Address = organizationAddress,
            Symbol = symbol
        });
        currentBalance.LockedBalance.ShouldBe(0);
        currentBalance.Balance.ShouldBe(previousBalance.Balance);
        feeAmount += chargedAmount;
        feeAddressBalance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = FeeAddress
        });
        feeAddressBalance.Balance.ShouldBe(feeAmount);
    }

    [Fact]
    public async Task ChargeWithExceptionTest()
    {
        await InitializeContractAsync();

        var symbol = "ELF";
        var amount = 100_00000000;
        var transactionResult = await DepositAsync(symbol, amount);
        var organizationAddress = GetOrganizationCreated(transactionResult).Address;
        
        var lockedAmount = 10_00000000;
        var orderId = "1";
        
        await UserBillingContractStub.Lock.SendAsync(new LockInput
        {
            Symbol = symbol,
            Amount = lockedAmount,
            OrderId = orderId
        });
        
        var chargedAmount = 5_00000000;
        var unlockedAmount = 3_00000000;
        var result = await AdminBillingContractStub.Charge.SendWithExceptionAsync(new ChargeInput
        {
            Adderss = organizationAddress,
            Symbol = symbol,
            ChargeAmount = chargedAmount,
            UnlockAmount = unlockedAmount
        });
        result.TransactionResult.Error.ShouldContain("No Permission");
        
        result = await TreasurerBillingContractStub.Charge.SendWithExceptionAsync(new ChargeInput
        {
            Adderss = organizationAddress,
            Symbol = symbol,
            ChargeAmount = -chargedAmount,
            UnlockAmount = unlockedAmount
        });
        result.TransactionResult.Error.ShouldContain("Invalid charge amount");
        
        result = await TreasurerBillingContractStub.Charge.SendWithExceptionAsync(new ChargeInput
        {
            Adderss = organizationAddress,
            Symbol = symbol,
            ChargeAmount = chargedAmount,
            UnlockAmount = -unlockedAmount
        });
        result.TransactionResult.Error.ShouldContain("Invalid unlock amount");
        
        result = await TreasurerBillingContractStub.Charge.SendWithExceptionAsync(new ChargeInput
        {
            Adderss = organizationAddress,
            Symbol = symbol,
            ChargeAmount = 0,
            UnlockAmount = 0
        });
        result.TransactionResult.Error.ShouldContain("ChargeAmount and UnlockAmount cannot both be 0");
        
        result = await TreasurerBillingContractStub.Charge.SendWithExceptionAsync(new ChargeInput
        {
            Adderss = organizationAddress,
            Symbol = symbol,
            ChargeAmount = lockedAmount,
            UnlockAmount = unlockedAmount
        });
        result.TransactionResult.Error.ShouldContain("Insufficient locked balance");
    }
        
    private async Task InitializeContractAsync()
    {
        var input = new InitializeInput()
        {
            Admin = AdminAddress,
            Treasurer = TreasurerAddress,
            FeeAddress = FeeAddress,
            Symbols = new SymbolList
            {
                Value =  { Symbols}
            }
        };
            
        await AdminBillingContractStub.Initialize.SendAsync(input);
    }
    
    private async Task<TransactionResult> DepositAsync(string symbol, long amount)
    {
        var userBeforeBalance = await UserTokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = UserAddress
        });
        await UserTokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Spender = ContractAddress,
            Symbol = symbol,
            Amount = amount
        });
        
        var result = await UserBillingContractStub.Deposit.SendAsync(new DepositInput
        {
            Symbol = symbol,
            Amount = amount
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        
        var userAfterBalance =await UserTokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
        {
            Symbol = symbol,
            Owner = UserAddress
        });
        
        userBeforeBalance.Balance.ShouldBe(userAfterBalance.Balance + amount);
        return result.TransactionResult;
    }
    
    private OrganizationCreated GetOrganizationCreated(TransactionResult transactionResult)
    {
        var organizationCreatedLogEvent = transactionResult.Logs.FirstOrDefault(l => l.Name == "OrganizationCreated");
        if (organizationCreatedLogEvent == null)
        {
            return null;
        }
        var organizationCreated = new OrganizationCreated();
        organizationCreated.MergeFrom(organizationCreatedLogEvent);
        return organizationCreated;
    }
}