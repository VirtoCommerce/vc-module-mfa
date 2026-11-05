angular.module('virtoCommerce.mfa')
    .controller('virtoCommerce.mfa.mfaModeWidgetController', ['$scope', 'platformWebApp.bladeNavigationService', 'virtoCommerce.mfa.mfaApi',
        function ($scope, bladeNavigationService, mfaApi) {
            var blade = $scope.widget.blade;
            var subjectType = $scope.widget.subjectType;

            function refresh() {
                if (!$scope.currentEntity || !$scope.currentEntity.id) {
                    return;
                }

                var getMode = subjectType === 'organization' ? mfaApi.getOrganizationMode : mfaApi.getUserMode;
                getMode({ id: $scope.currentEntity.id }, function (result) {
                    $scope.mode = result.mode;
                });
            }

            $scope.openBlade = function () {
                if (!$scope.currentEntity || !$scope.currentEntity.id) {
                    return;
                }

                var newBlade = {
                    id: 'mfaMode',
                    subjectType: subjectType,
                    subjectId: $scope.currentEntity.id,
                    subjectName: $scope.currentEntity.userName || $scope.currentEntity.name,
                    onChanged: function (mode) {
                        $scope.mode = mode;
                    },
                    controller: 'virtoCommerce.mfa.mfaModeController',
                    template: 'Modules/$(VirtoCommerce.MFA)/Scripts/blades/mfa-mode.tpl.html'
                };

                bladeNavigationService.showBlade(newBlade, blade);
            };

            // The blade loads its entity after the widget is created
            $scope.$watch(function () {
                return blade.currentEntity && blade.currentEntity.id;
            }, function () {
                $scope.currentEntity = blade.currentEntity;
                refresh();
            });
        }]);
