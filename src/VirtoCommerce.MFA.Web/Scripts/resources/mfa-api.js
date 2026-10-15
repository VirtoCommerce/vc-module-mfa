angular.module('virtoCommerce.mfa')
    .factory('virtoCommerce.mfa.mfaApi', ['$resource', function ($resource) {
        return $resource('api/mfa', {}, {
            sendSignInCode: { method: 'POST', url: 'api/mfa/signin/send' },
            verifySignInCode: { method: 'POST', url: 'api/mfa/signin/verify' }
        });
    }]);
