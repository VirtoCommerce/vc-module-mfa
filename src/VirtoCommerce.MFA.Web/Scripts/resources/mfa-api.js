angular.module('virtoCommerce.mfa')
    .factory('virtoCommerce.mfa.mfaApi', ['$resource', function ($resource) {
        return $resource('api/mfa', {}, {
            sendSignInCode: { method: 'POST', url: 'api/mfa/signin/send' },
            verifySignInCode: { method: 'POST', url: 'api/mfa/signin/verify' },
            getUserMode: { method: 'GET', url: 'api/mfa/modes/users/:id' },
            setUserMode: { method: 'PUT', url: 'api/mfa/modes/users/:id' },
            getOrganizationMode: { method: 'GET', url: 'api/mfa/modes/organizations/:id' },
            setOrganizationMode: { method: 'PUT', url: 'api/mfa/modes/organizations/:id' },
            getUserStatus: { method: 'GET', url: 'api/mfa/modes/users/:id/status' },
            getOrganizationStatus: { method: 'GET', url: 'api/mfa/modes/organizations/:id/status' }
        });
    }]);
