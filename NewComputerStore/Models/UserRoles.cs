namespace NewComputerStore.Models
{
    /// <summary>
    /// Centralized role constants — use these instead of raw strings everywhere.
    /// </summary>
    public static class UserRoles
    {
        public const string Admin            = "Admin";
        public const string Customer         = "Customer";
        public const string Staff            = "Staff";
        public const string WarehouseManager = "WarehouseManager";
        public const string EmployeeManager  = "EmployeeManager";
        public const string CustomerSupport  = "CustomerSupport";

        /// <summary>All roles that are considered internal staff (non-customer).</summary>
        public static readonly string[] StaffRoles =
        {
            Admin, Staff, WarehouseManager, EmployeeManager, CustomerSupport
        };

        /// <summary>Roles that a Staff Management admin can assign to new staff accounts.</summary>
        public static readonly string[] AssignableStaffRoles =
        {
            Staff, WarehouseManager, EmployeeManager, Admin, CustomerSupport
        };
    }
}
