angular.module('virtoCommerce.mfa')
    .controller('virtoCommerce.mfa.twoFactorSignInController', [
        '$scope', '$state', '$stateParams', '$translate', 'platformWebApp.authService', 'virtoCommerce.mfa.mfaApi',
        function ($scope, $state, $stateParams, $translate, authService, mfaApi) {
            var CODE_PATTERN = /^\d{6}$/;
            var FINAL_ERRORS = ['invalid_two_factor_token', 'lockout_disabled', 'mfa_provider_not_available', 'sign_in_not_allowed', 'user_is_locked_out'];

            $scope.model = { code: '' };
            $scope.destination = null;
            $scope.provider = null;
            $scope.authError = null;
            $scope.notice = null;
            $scope.isFinalError = false;
            $scope.inProgress = false;

            $scope.isCodeComplete = function () {
                return CODE_PATTERN.test($scope.model.code || '');
            };

            $scope.sendCode = function (isResend) {
                if ($scope.inProgress) {
                    return;
                }

                $scope.authError = null;
                $scope.notice = null;
                $scope.inProgress = true;

                mfaApi.sendSignInCode({ provider: $scope.provider }, function (result) {
                    $scope.inProgress = false;

                    if (!result.succeeded) {
                        showError(result.error);
                        return;
                    }

                    var provider = (result.providers || []).find(function (x) { return x.name === result.provider; });
                    $scope.provider = result.provider;
                    $scope.destination = provider ? provider.destination : null;

                    if (isResend) {
                        $scope.model.code = '';
                        $scope.notice = $translate.instant('mfa.sign-in.code-resent');
                    }
                }, onTechnicalError);
            };

            $scope.signIn = function () {
                if (!$scope.isCodeComplete() || $scope.inProgress) {
                    return;
                }

                $scope.authError = null;
                $scope.notice = null;
                $scope.inProgress = true;

                mfaApi.verifySignInCode({ code: $scope.model.code, provider: $scope.provider, rememberMe: !!$stateParams.rememberMe }, function (result) {
                    if (result.succeeded) {
                        authService.fillAuthData().finally(function () {
                            $scope.inProgress = false;
                        });
                        return;
                    }

                    $scope.inProgress = false;
                    $scope.model.code = '';
                    showError(result.error);
                }, onTechnicalError);
            };

            $scope.backToSignIn = function () {
                $state.go('loginDialog');
            };

            function showError(error) {
                var code = error && error.code;

                $scope.isFinalError = FINAL_ERRORS.indexOf(code) >= 0;

                var key = 'mfa.sign-in.errors.' + code;
                var message = code ? $translate.instant(key) : null;

                $scope.authError = message && message !== key
                    ? message
                    : $translate.instant('platform.blades.login.errors.login-failed');
            }

            function onTechnicalError(response) {
                $scope.inProgress = false;
                $scope.authError = $translate.instant('platform.blades.login.errors.technical-error', { code: response && response.status });
            }

            $scope.sendCode(false);
        }
    ]);
