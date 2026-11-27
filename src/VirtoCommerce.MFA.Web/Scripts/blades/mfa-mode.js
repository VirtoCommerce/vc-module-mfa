angular.module('virtoCommerce.mfa')
    .controller('virtoCommerce.mfa.mfaModeController', ['$scope', 'virtoCommerce.mfa.mfaApi', 'platformWebApp.authService',
        function ($scope, mfaApi, authService) {
            var blade = $scope.blade;
            var isOrganization = blade.subjectType === 'organization';
            var managePermission = 'mfa:manage';
            var statusRequest = 0;

            // mfa:read opens the blade to view the mode, changing it needs mfa:manage
            blade.isOrganization = isOrganization;
            blade.backofficeExempt = false;
            blade.originalBackofficeExempt = false;
            blade.canManage = authService.checkPermission(managePermission);

            blade.title = isOrganization ? 'mfa.blades.mode.organization-title' : 'mfa.blades.mode.user-title';
            blade.titleValues = { name: blade.subjectName };
            blade.subtitle = 'mfa.blades.mode.subtitle';

            $scope.modes = ['Off', 'Enabled', 'Required'];

            function setMode(mode) {
                blade.originalMode = mode;
                blade.mode = mode;
                blade.isLoading = false;
                refreshStatus();
                if (blade.onChanged) {
                    blade.onChanged(mode);
                }
            }

            blade.refresh = function () {
                blade.isLoading = true;
                var getMode = isOrganization ? mfaApi.getOrganizationMode : mfaApi.getUserMode;
                getMode({ id: blade.subjectId }, function (result) {
                    if (isOrganization) {
                        setMode(result.mode);
                    } else {
                        mfaApi.getBackofficeExemption({ id: blade.subjectId }, function (exemption) {
                            blade.backofficeExempt = blade.originalBackofficeExempt = exemption.isExempt;
                            setMode(result.mode);
                        }, function () { blade.isLoading = false; });
                    }
                }, function () { blade.isLoading = false; });
            };

            // The status follows the selected mode, so the administrator sees the effect before saving
            function refreshStatus() {
                statusRequest += 1;
                var request = statusRequest;
                blade.status = null;
                var getStatus = isOrganization ? mfaApi.getOrganizationStatus : mfaApi.getUserStatus;
                getStatus({ id: blade.subjectId, mode: blade.mode }, function (result) {
                    if (request === statusRequest) {
                        blade.status = result;
                    }
                });
            }

            function isDirty() {
                return blade.mode !== blade.originalMode || $scope.isExemptionDirty();
            }

            $scope.isExemptionDirty = function () {
                return !isOrganization && blade.backofficeExempt !== blade.originalBackofficeExempt;
            };

            $scope.getBackofficeReasonKey = function () {
                var reason = blade.status.isRequiredByIdentity ? 'Identity' :
                    (blade.status.isBackofficeExempt ? 'BackofficeExempt' :
                        (blade.status.isRequiredForBackoffice ? 'Global' : 'GlobalOff'));
                return 'mfa.blades.mode.status.reasons.' + reason;
            };

            $scope.isDirty = isDirty;

            $scope.getStoreModeKey = function (storeMode) {
                return 'mfa.store-modes.' + (storeMode === 'Use global setting' ? 'UseGlobalSetting' : storeMode);
            };

            // In these stores the modes of users and organizations decide
            function isChosenStoreMode(storeMode) {
                return storeMode === 'AdminManaged' || storeMode === 'CustomerChoice';
            }

            function getRequiredReason(store) {
                if (store.isRequiredByUserStore) {
                    return 'UserStore';
                }

                if (store.isDisabledModeSkipped) {
                    return 'DisabledSkipped';
                }

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
                    if (store.isDisabledModeSkipped) {
                        return blade.status.isBackofficeExempt && blade.status.isGloballyEnabled ? 'BackofficeExempt' : 'GlobalOff';
                    }
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

            $scope.getReasonParams = function () {
                var userStoreId = blade.status.userStoreId;
                var userStore = (blade.status.stores || []).filter(function (x) { return x.storeId === userStoreId; })[0];

                return { store: userStore ? (userStore.storeName || userStore.storeId) : userStoreId };
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
                        statusRequest += 1;
                        blade.status = null;
                        function saveExemption() {
                            if ($scope.isExemptionDirty()) {
                                mfaApi.setBackofficeExemption({ id: blade.subjectId }, { isExempt: blade.backofficeExempt }, function (exemption) {
                                    blade.originalBackofficeExempt = blade.backofficeExempt = exemption.isExempt;
                                    blade.refresh();
                                }, function () { blade.isLoading = false; });
                            } else {
                                blade.refresh();
                            }
                        }

                        if (blade.mode !== blade.originalMode) {
                            var setModeApi = isOrganization ? mfaApi.setOrganizationMode : mfaApi.setUserMode;
                            setModeApi({ id: blade.subjectId }, { mode: blade.mode }, function (result) {
                                blade.originalMode = blade.mode = result.mode;
                                saveExemption();
                            }, function () { blade.isLoading = false; });
                        } else {
                            saveExemption();
                        }
                    },
                    canExecuteMethod: function () { return !blade.isLoading && isDirty(); },
                    permission: managePermission
                },
                {
                    name: 'platform.commands.reset',
                    icon: 'fa fa-undo',
                    executeMethod: function () {
                        blade.mode = blade.originalMode;
                        blade.backofficeExempt = blade.originalBackofficeExempt;
                    },
                    canExecuteMethod: function () { return !blade.isLoading && isDirty(); },
                    permission: managePermission
                }
            ];

            blade.refresh();
        }]);
