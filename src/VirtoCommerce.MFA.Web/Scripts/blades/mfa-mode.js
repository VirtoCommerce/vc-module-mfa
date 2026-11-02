angular.module('virtoCommerce.mfa')
    .controller('virtoCommerce.mfa.mfaModeController', ['$scope', 'virtoCommerce.mfa.mfaApi', 'platformWebApp.authService',
        function ($scope, mfaApi, authService) {
            var blade = $scope.blade;
            var isOrganization = blade.subjectType === 'organization';
            var managePermission = 'mfa:manage';
            var statusRequest = 0;

            // mfa:read opens the blade to view the mode, changing it needs mfa:manage
            blade.canManage = authService.checkPermission(managePermission);

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
                statusRequest += 1;
                var request = statusRequest;
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

            // In these stores the modes of users and organizations decide
            function isChosenStoreMode(storeMode) {
                return storeMode === 'AdminManaged' || storeMode === 'CustomerChoice';
            }

            function getRequiredReason(store) {
                if (store.requiredBy === 'Organization') {
                    return isOrganization ? 'OrganizationSelf' : 'Organization';
                }

                if (store.requiredBy === 'User') {
                    return blade.mode === 'Required' ? 'UserRequired' : 'UserEnabled';
                }

                return store.requiredBy;
            }

            function getNotRequiredReason(store) {
                if (store.storeMode === 'Disabled') {
                    return 'Disabled';
                }

                if (isChosenStoreMode(store.storeMode)) {
                    return (isOrganization ? 'Members' : '') + store.storeMode;
                }

                return 'GlobalOff';
            }

            // In these stores an organization that requires nothing leaves the choice to each member
            $scope.isPerMember = function (store) {
                return isOrganization && !store.isRequired && isChosenStoreMode(store.storeMode);
            };

            $scope.getReasonKey = function (store) {
                return 'mfa.blades.mode.status.reasons.' + (store.isRequired ? getRequiredReason(store) : getNotRequiredReason(store));
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
                    permission: managePermission
                },
                {
                    name: 'platform.commands.reset',
                    icon: 'fa fa-undo',
                    executeMethod: function () {
                        blade.mode = blade.originalMode;
                    },
                    canExecuteMethod: isDirty,
                    permission: managePermission
                }
            ];

            blade.refresh();
        }]);
