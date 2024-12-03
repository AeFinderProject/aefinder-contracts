using System.Collections.Generic;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using AElf.Testing.TestBase;
using AElf.Types;
using Volo.Abp.Threading;

namespace AeFinder.Contracts
{
    // The Module class load the context required for unit testing
    public class Module : ContractTestModule<BillingContract>
    {
        
    }
    
    // The TestBase class inherit ContractTestBase class, it defines Stub classes and gets instances required for unit testing
    public class TestBase : ContractTestBase<Module>
    {
        // The Stub class for unit testing
        internal readonly BillingContractContainer.BillingContractStub AdminBillingContractStub;
        
        internal readonly BillingContractContainer.BillingContractStub OtherAdminBillingContractStub;

        internal readonly BillingContractContainer.BillingContractStub TreasurerBillingContractStub;
        
        internal readonly BillingContractContainer.BillingContractStub UserBillingContractStub;

        internal readonly TokenContractContainer.TokenContractStub TokenContractStub;
        internal readonly TokenContractContainer.TokenContractStub UserTokenContractStub;
        
        // A key pair that can be used to interact with the contract instance
        private ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;
        private ECKeyPair AdminKeyPair => Accounts[0].KeyPair;
        protected Address AdminAddress => Accounts[0].Address;
        private ECKeyPair NewAdminKeyPair => Accounts[1].KeyPair;
        protected Address NewAdminAddress => Accounts[1].Address;
        
        private ECKeyPair TreasurerKeyPair => Accounts[2].KeyPair;
        protected Address TreasurerAddress => Accounts[2].Address;
        protected Address NewTreasurerAddress => Accounts[3].Address;
            
        protected Address FeeAddress => Accounts[4].Address;
        protected Address NewFeeAddress => Accounts[5].Address;
        
        protected Address UserAddress => Accounts[6].Address;
        protected ECKeyPair UserKeyPair => Accounts[6].KeyPair;
        
        protected Address OtherUserAddress => Accounts[7].Address;

        protected List<string> Symbols => new()
        {
            "USDT",
            "ELF"
        };

        public TestBase()
        {
            AdminBillingContractStub = GetBillingContractContractStub(AdminKeyPair);
            OtherAdminBillingContractStub = GetBillingContractContractStub(NewAdminKeyPair);
            TreasurerBillingContractStub = GetBillingContractContractStub(TreasurerKeyPair);
            UserBillingContractStub = GetBillingContractContractStub(UserKeyPair);
            TokenContractStub = GetTokenContractStub(AdminKeyPair);
            UserTokenContractStub = GetTokenContractStub(UserKeyPair);
            InitBalance();
        }

        private void InitBalance()
        {
            AsyncHelper.RunSync(async () => await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                To = UserAddress,
                Symbol = "ELF",
                Amount = 10000_00000000
            }));
        }

        private BillingContractContainer.BillingContractStub GetBillingContractContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<BillingContractContainer.BillingContractStub>(ContractAddress, senderKeyPair);
        }

        private TokenContractContainer.TokenContractStub GetTokenContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress, senderKeyPair);
        }
        
    }
    
}