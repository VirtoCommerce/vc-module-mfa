using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.MFA.Xapi.Schemas;

public class MfaSettingsType : ExtendableGraphType<MfaSettings>
{
    public MfaSettingsType()
    {
        Name = "MfaSettings";

        Field(x => x.StoreId);
        Field(x => x.StoreMode).Description("UseGlobalSetting, Enabled, Disabled, AdminManaged or CustomerChoice.");
        Field(x => x.IsRequired).Description("Whether the password sign-in to the store requires the code.");
        Field(x => x.RequiredBy).Description("None, Global, Store, Organization or User.");
        Field(x => x.UserMode).Description("Off, Enabled (by the user) or Required (by the administrator).");
        Field(x => x.CanChangeUserMode);
        Field(x => x.OrganizationId, nullable: true);
        Field(x => x.OrganizationMode, nullable: true).Description("Off, Enabled (by the organization maintainer) or Required (by the administrator).");
        Field(x => x.CanChangeOrganizationMode);
        Field(x => x.IsProviderAvailable).Description("Whether a code can be delivered to the user, for example the email is confirmed.");
        Field(x => x.Provider, nullable: true);
        Field(x => x.Destination, nullable: true);
    }
}
