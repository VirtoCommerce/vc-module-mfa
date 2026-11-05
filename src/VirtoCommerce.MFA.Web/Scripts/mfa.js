var moduleName = 'virtoCommerce.mfa';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .config(['$stateProvider', function ($stateProvider) {
        $stateProvider.state('twoFactorDialog', {
            url: '/sign-in-code',
            templateUrl: 'Modules/$(VirtoCommerce.MFA)/Scripts/sign-in/two-factor-sign-in.tpl.html',
            controller: 'virtoCommerce.mfa.twoFactorSignInController',
            params: {
                rememberMe: false
            },
            data: {
                allowAnonymous: true,
                twoFactorSignIn: true
            }
        });
    }])
    .run(['platformWebApp.signInLogTypes', 'platformWebApp.widgetService', 'platformWebApp.authService',
        function (signInLogTypes, widgetService, authService) {
            signInLogTypes.register({ label: 'mfa.sign-in-log.type-mfa', value: 'MFA' });

            function createModeWidget(subjectType) {
                return {
                    controller: 'virtoCommerce.mfa.mfaModeWidgetController',
                    template: 'Modules/$(VirtoCommerce.MFA)/Scripts/widgets/mfa-mode-widget.tpl.html',
                    subjectType: subjectType,
                    // The container checks this once, before the blade loads its entity, so it can't depend on currentEntity
                    isVisible: function (blade) {
                        return !blade.isNew && authService.checkPermission('mfa:read');
                    }
                };
            }

            widgetService.registerWidget(createModeWidget('user'), 'accountDetail');
            widgetService.registerWidget(createModeWidget('organization'), 'organizationDetail2');
        }]);
