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
            data: { allowAnonymous: true }
        });
    }]);
