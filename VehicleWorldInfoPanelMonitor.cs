﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ColossalFramework.UI;
using TrackIt.API;

namespace TrackIt
{
    /// <summary>
    /// Unity behaviour to handle intial display and updates for the companion window for CityServiceVehicleWorldInfoPanel.
    /// </summary>
    public class VehicleWorldInfoPanelMonitor : MonoBehaviour
    {
        internal struct TrackingRow
        {
            internal UIPanel Panel;
            internal UILabel Description;
            internal UIProgressBar ProgressBar;
            internal UILabel Percentage;
        }

        private CityServiceVehicleWorldInfoPanel _cityServiceVehicleWorldInfoPanel;
        private ushort _cachedVehicleID;

        private const string _namePrefix = "VehicleWorldInfoPanel";
        private const int _chartWidth = 60;
        private const int _chartHeight = 60;

        private UIPanel _containerPanel; // main top-level container
        private Vector2 _containerPadding = new Vector2(4, 4);
        private UIPanel _vehicleCargoPanel;
        private Vector2 _vehicleCargoPadding = new Vector2(8, 8);
        private CargoUIChart _vehicleCargoChart; // grouped category resource chart within _vehicleCargoPanel
        private IList<TrackingRow> _vehicleCargoContents; // internal map whose offset matches index in UIUtils.CargoBasicResourceGroups

        public void Start()
        {
            try
            {
                CreateUI();
            }
            catch (Exception e)
            {
                LogUtil.LogException(e);
            }
        }

        public void Update()
        {
            try
            {
                if (!_cityServiceVehicleWorldInfoPanel.component.isVisible)
                {
                    ResetCache();
                }
                else
                {
                    UpdateData();
                }
            }
            catch (Exception e)
            {
                LogUtil.LogException(e);
            }
        }

        public void OnDestroy()
        {
            if (_containerPanel != null)
            {
                Destroy(_containerPanel);
            }
        }

        private void CreateUI()
        {
            _cityServiceVehicleWorldInfoPanel = UIView.library.Get<CityServiceVehicleWorldInfoPanel>(typeof(CityServiceVehicleWorldInfoPanel).Name);
            UIProgressBar loadProgressBar = _cityServiceVehicleWorldInfoPanel?.component.Find<UIProgressBar>("LoadBar");
            if (_cityServiceVehicleWorldInfoPanel == null)
            {
                LogUtil.LogError("Unable to find CityServiceVehicleWorldInfoPanel");
                return;
            }

            UILabel totalLoadInfoPercentageLabel = _cityServiceVehicleWorldInfoPanel?.Find<UILabel>("LoadInfo");

            // InfoBubbleVehicle used on main panel, but it contains a title area which is not suitable here (use next best option - GenericTab)
            _containerPanel = UIUtils.CreateWorldInfoCompanionPanel(_cityServiceVehicleWorldInfoPanel.component, _namePrefix, "GenericTab");
            _containerPanel.autoLayout = true;
            _containerPanel.autoLayoutDirection = LayoutDirection.Vertical;
            _containerPanel.autoLayoutPadding = new RectOffset(6, 6, 6, 6); // TODO: account for in resize!
            _containerPanel.width = _containerPanel.parent.width;

            _vehicleCargoContents = new List<TrackingRow>(UIUtils.CargoBasicResourceGroups.Count);

            _vehicleCargoPanel = UIUtils.CreatePanel(_containerPanel, _namePrefix + "Cargo");
            _vehicleCargoPanel.autoLayout = true;
            _vehicleCargoPanel.autoLayoutDirection = LayoutDirection.Vertical;

            _vehicleCargoChart = UIUtils.CreateCargoGroupedResourceChart(_vehicleCargoPanel, _namePrefix + "CargoGroupedResourceChart");
            _vehicleCargoChart.size = new Vector2(_chartWidth, _chartHeight);
            _vehicleCargoChart.anchor = UIAnchorStyle.CenterHorizontal;

            ResourceCategoryType resourceCategoryType;
            UIPanel vehicleCargoContentRow;
            for (int i = 0; i < UIUtils.CargoBasicResourceGroups.Count; i++)
            {
                resourceCategoryType = UIUtils.CargoBasicResourceGroups[i];

                vehicleCargoContentRow = UIUtils.CreatePanel(_vehicleCargoPanel, "VehicleCargo" + resourceCategoryType + "Row");
                vehicleCargoContentRow.autoLayout = true;
                vehicleCargoContentRow.autoLayoutDirection = LayoutDirection.Vertical;
                vehicleCargoContentRow.width = _vehicleCargoPanel.width;

                string localeID = UIUtils.GetLocaleID(resourceCategoryType);
                TrackingRow trackingRow = new TrackingRow
                {
                    Panel = vehicleCargoContentRow,
                    Description = UIUtils.CreateLabel(vehicleCargoContentRow, "VehicleCargo" + resourceCategoryType + "Label", localeID),
                    ProgressBar = UIUtils.CreateCargoProgressBar(vehicleCargoContentRow, "VehicleCargo" + resourceCategoryType + "Amount"),
                    Percentage = UIUtils.CreateLabel(vehicleCargoContentRow, "VehicleCargo" + resourceCategoryType + "Percent", null)
                };

                UIUtils.CopyTextStyleAttributes(totalLoadInfoPercentageLabel, trackingRow.Percentage);
                trackingRow.Percentage.AlignTo(trackingRow.ProgressBar, UIAlignAnchor.TopRight);
                trackingRow.Percentage.relativePosition = new Vector3(trackingRow.ProgressBar.width + 20f, 0f);

                trackingRow.Panel.width = _containerPanel.width;
                trackingRow.Panel.height = 40f;
                trackingRow.Description.textScale = 0.8125f;
                trackingRow.Description.width = 80;
                trackingRow.Description.autoHeight = true;
                trackingRow.ProgressBar.width = loadProgressBar?.width ?? 293;
                trackingRow.ProgressBar.minValue = 0;   // scale as a percent value
                trackingRow.ProgressBar.maxValue = 100;

                _vehicleCargoContents.Add(trackingRow);
            }

            // TODO: handle with configuration BottomLeft if conflicts from other mods
            // TODO: change width to either be same as panel width itself (bottom) or trimmed (right)
            _containerPanel.AlignTo(_cityServiceVehicleWorldInfoPanel.component, UIAlignAnchor.TopRight);
            _containerPanel.relativePosition = new Vector3(_containerPanel.parent.width + 5f, 0);
        }

        private bool IsInitialized()
        {
            return _containerPanel != null;
        }

        /// <summary>
        /// This method is for change detection so updates to the UI are done only when needed.
        /// </summary>
        private void ResetCache()
        {
            _cachedVehicleID = 0;
        }

        private void UpdateData()
        {
            var vehicleID = WorldInfoPanel.GetCurrentInstanceID().Vehicle;
            if (vehicleID == 0 || _cachedVehicleID == vehicleID || !IsInitialized())
            {
                return;
            }
            IDictionary<ResourceCategoryType, int> vehicleCargoCategoryTotals = GameEntityDataExtractor.GetVehicleCargoBasicResourceTotals(vehicleID);
            if (vehicleCargoCategoryTotals.Count != 0)
            {
                int grandTotal = vehicleCargoCategoryTotals.Values.Sum();
#if DEBUG
                LogUtil.LogInfo($"Vehicle Cargo Total: {grandTotal}" + ", Groups: {" +
                    vehicleCargoCategoryTotals.Select(kv => kv.Key + ": " +
                    kv.Value).Aggregate((p, c) => p + ": " + c) + "}");
#endif
                ResourceCategoryType resourceCategoryType;
                TrackingRow vehicleCargoContentRow;
                int categoryTotal = 0;
                for (int i = 0; i < UIUtils.CargoBasicResourceGroups.Count; i++)
                {
                    resourceCategoryType = UIUtils.CargoBasicResourceGroups[i];
                    vehicleCargoContentRow = _vehicleCargoContents[i];
                    if (vehicleCargoContentRow.Description.isLocalized && vehicleCargoCategoryTotals.TryGetValue(resourceCategoryType, out categoryTotal))
                    {
                        UpdateProgressBar(vehicleCargoContentRow.ProgressBar,
                            UIUtils.GetResourceCategoryColor(resourceCategoryType),
                            categoryTotal,
                            grandTotal);
                        vehicleCargoContentRow.Percentage.text = LocaleFormatter.FormatPercentage((int)vehicleCargoContentRow.ProgressBar.value);
                        vehicleCargoContentRow.Panel.Show();
                    }
                    else
                    {
                        vehicleCargoContentRow.Panel.Hide();
                    }
                }
                _vehicleCargoChart.SetValues(vehicleCargoCategoryTotals);
                _vehicleCargoChart.Show();

                _vehicleCargoPanel.FitChildren(_vehicleCargoPadding);

                _containerPanel.FitChildren(_containerPadding);
                _containerPanel.Show();
            }
            else
            {
#if DEBUG
                LogUtil.LogInfo($"No cargo resources found for vehicle {vehicleID}");
#endif
                _vehicleCargoChart.ResetValues();
                _vehicleCargoChart.Hide();
                _containerPanel.Hide();
            }
            _cachedVehicleID = vehicleID;
        }

        private void UpdateProgressBar(UIProgressBar progressBar, Color color, int amount, int total)
        {
            if (total > 0)
            {
                progressBar.tooltip = UIUtils.FormatCargoValue(amount);
                progressBar.value = Mathf.Clamp((amount * 100.0f / total), 0f, 100f);
            }
            else
            {
                progressBar.tooltip = null;
                progressBar.value = 0;
            }
            progressBar.progressColor = color;
        }
    }
}
