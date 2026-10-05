namespace VirtoCommerce.MFA.Core;

public static class ModuleConstants
{
    public static class Security
    {
        public static class Permissions
        {
            public const string Create = "mfa:create";
            public const string Read = "mfa:read";
            public const string Update = "mfa:update";
            public const string Delete = "mfa:delete";

            public static string[] AllPermissions { get; } =
            [
                Create,
                Read,
                Update,
                Delete,
            ];
        }
    }
}
