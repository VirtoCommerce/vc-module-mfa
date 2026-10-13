angular.module('virtoCommerce.mfa')
    .controller('virtoCommerce.mfa.twoFactorSignInController', [
        '$scope', '$state', '$stateParams', '$translate', '$interval', 'platformWebApp.authService', 'virtoCommerce.mfa.mfaApi',
        function ($scope, $state, $stateParams, $translate, $interval, authService, mfaApi) {
            var CODE_PATTERN = /^\d{6}$/;
            var RESEND_TOO_SOON = 'resend_too_soon';
            var FINAL_ERRORS = ['invalid_two_factor_token', 'lockout_disabled', 'mfa_provider_not_available', 'sign_in_not_allowed', 'user_is_locked_out', 'user_is_temporary_locked_out', 'login_failed'];
            var resendDeadline = 0;
            var resendTimer = null;

            $scope.model = { code: '' };
            $scope.destination = null;
            $scope.provider = null;
            $scope.authError = null;
            $scope.notice = null;
            $scope.isFinalError = false;
            $scope.inProgress = false;
            $scope.resendSecondsLeft = 0;

            $scope.isCodeComplete = function () {
                return CODE_PATTERN.test($scope.model.code || '');
            };

            $scope.sendCode = function (isResend) {
                if ($scope.inProgress || (isResend && $scope.resendSecondsLeft > 0)) {
                    return;
                }

                $scope.authError = null;
                $scope.notice = null;
                $scope.inProgress = true;

                mfaApi.sendSignInCode({ provider: $scope.provider }, function (result) {
                    $scope.inProgress = false;
                    startResendCountdown(result.resendSecondsRemaining);

                    var isResendTooSoon = !result.succeeded && result.error && result.error.code === RESEND_TOO_SOON;
                    if (!result.succeeded && !isResendTooSoon) {
                        showError(result.error);
                        return;
                    }

                    var provider = (result.providers || []).find(function (x) {
                        return x.name === result.provider;
                    });
                    $scope.provider = result.provider;
                    $scope.destination = provider ? provider.destination : null;

                    if (isResend && result.succeeded) {
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

            function startResendCountdown(seconds) {
                if (!seconds) {
                    return;
                }

                resendDeadline = Date.now() + seconds * 1000;
                updateResendCountdown();

                if (!resendTimer) {
                    resendTimer = $interval(updateResendCountdown, 1000);
                }
            }

            function updateResendCountdown() {
                $scope.resendSecondsLeft = Math.max(0, Math.ceil((resendDeadline - Date.now()) / 1000));

                if ($scope.resendSecondsLeft === 0 && resendTimer) {
                    $interval.cancel(resendTimer);
                    resendTimer = null;
                }
            }

            $scope.$on('$destroy', function () {
                if (resendTimer) {
                    $interval.cancel(resendTimer);
                }
            });

            function onTechnicalError(response) {
                $scope.inProgress = false;
                $scope.authError = $translate.instant('platform.blades.login.errors.technical-error', { code: response && response.status });
            }

            $scope.sendCode(false);
        }
    ]);
