angular.module('virtoCommerce.mfa')
    .controller('virtoCommerce.mfa.mfaModeController', ['$scope', 'virtoCommerce.mfa.mfaApi', 'platformWebApp.authService',
        function ($scope, mfaApi, authService) {
            var blade = $scope.blade;
            var isOrganization = blade.subjectType === 'organization';
            var statusRequest = 0;

            // mfa:read opens the blade to view the mode, changing it needs mfa:manage
            blade.canManage = authService.checkPermission('mfa:manage');

            blade.title = isOrganization ? 'mfa.blades.mode.organization-title' : 'mfa.blades.mode.user-title';
            blade.titleValues = { name: blade.subjectName };
            blade.subtitle = 'mfa.blades.mode.subtitle';

            $scope.modes = ['Off', 'Enabled', 'Required'];

            function setMode(mode) {
                blade.originalMode = mode;
                blade.mode = mode;
                blade.isLoading = false;
            }

            blade.refresh = function () {
                blade.isLoading = true;
                var getMode = isOrganization ? mfaApi.getOrganizationMode : mfaApi.getUserMode;
                getMode({ id: blade.subjectId }, function (result) {
                    setMode(result.mode);
                });
            };

            // The status follows the selected mode, so the administrator sees the effect before saving
            function refreshStatus() {
                var request = ++statusRequest;
                var getStatus = isOrganization ? mfaApi.getOrganizationStatus : mfaApi.getUserStatus;
                getStatus({ id: blade.subjectId, mode: blade.mode }, function (result) {
                    if (request === statusRequest) {
                        blade.status = result;
                    }
                });
            }

            function isDirty() {
                return blade.mode !== blade.originalMode;
            }

            $scope.isDirty = isDirty;

            $scope.getStoreModeKey = function (storeMode) {
                return 'mfa.store-modes.' + (storeMode === 'Use global setting' ? 'UseGlobalSetting' : storeMode);
            };

            // In these stores an organization that requires nothing leaves the choice to each member
            $scope.isPerMember = function (store) {
                return isOrganization && !store.isRequired && (store.storeMode === 'AdminManaged' || store.storeMode === 'CustomerChoice');
            };

            $scope.getReasonKey = function (store) {
                var reason;

                if (store.isRequired) {
                    if (store.requiredBy === 'Organization') {
                        reason = isOrganization ? 'OrganizationSelf' : 'Organization';
                    } else if (store.requiredBy === 'User') {
                        reason = blade.mode === 'Required' ? 'UserRequired' : 'UserEnabled';
                    } else {
                        reason = store.requiredBy;
                    }
                } else if (store.storeMode === 'Disabled') {
                    reason = 'Disabled';
                } else if (store.storeMode === 'AdminManaged' || store.storeMode === 'CustomerChoice') {
                    reason = (isOrganization ? 'Members' : '') + store.storeMode;
                } else {
                    reason = 'GlobalOff';
                }

                return 'mfa.blades.mode.status.reasons.' + reason;
            };

            $scope.$watch('blade.mode', function (mode) {
                if (mode) {
                    refreshStatus();
                }
            });

            blade.toolbarCommands = [
                {
                    name: 'platform.commands.save',
                    icon: 'fas fa-save',
                    executeMethod: function () {
                        blade.isLoading = true;
                        var setModeApi = isOrganization ? mfaApi.setOrganizationMode : mfaApi.setUserMode;
                        setModeApi({ id: blade.subjectId }, { mode: blade.mode }, function (result) {
                            setMode(result.mode);
                            if (blade.onChanged) {
                                blade.onChanged(result.mode);
                            }
                        }, function () {
                            blade.isLoading = false;
                        });
                    },
                    canExecuteMethod: isDirty,
                    permission: 'mfa:manage'
                },
                {
                    name: 'platform.commands.reset',
                    icon: 'fa fa-undo',
                    executeMethod: function () {
                        blade.mode = blade.originalMode;
                    },
                    canExecuteMethod: isDirty,
                    permission: 'mfa:manage'
                }
            ];

            blade.refresh();
        }]);
