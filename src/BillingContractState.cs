using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace AeFinder.Contracts
{
    /// <summary>
    /// Represents the state of the Billing Contract.
    /// </summary>
    public partial class BillingContractState : ContractState 
    {
        /// <summary>
        /// Indicates whether the contract has been initialized.
        /// </summary>
        public BoolState Initialized { get; set; }

        /// <summary>
        /// Stores the balance information for organizations by address and symbol.
        /// </summary>
        public MappedState<Address, string, long> Balances { get; set; }

        /// <summary>
        /// Stores the locked balance information for organizations by address and symbol.
        /// </summary>
        public MappedState<Address, string, long> LockedBalances { get; set; }

        /// <summary>
        /// Maps a user address to their organization.
        /// </summary>
        public MappedState<Address, Address> UserOrganizationMap { get; set; }

        /// <summary>
        /// Stores the organization information by organization address.
        /// </summary>
        public MappedState<Address, Organization> Organizations { get; set; }

        /// <summary>
        /// Stores the administrator's address.
        /// </summary>
        public SingletonState<Address> Admin { get; set; }

        /// <summary>
        /// Stores the treasurer's address.
        /// </summary>
        public SingletonState<Address> Treasurer { get; set; }

        /// <summary>
        /// Stores the list of fee symbols.
        /// </summary>
        public SingletonState<SymbolList> FeeSymbols { get; set; }

        /// <summary>
        /// Stores the fee address.
        /// </summary>
        public SingletonState<Address> FeeAddress { get; set; }
    }
}