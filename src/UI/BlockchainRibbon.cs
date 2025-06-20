using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.IO;

namespace RevitBlockchain.UI
{
    /// <summary>
    /// Creates the Revit ribbon interface for blockchain features
    /// </summary>
    public class BlockchainRibbon
    {
        private const string RIBBON_TAB = "Blockchain";
        private const string RIBBON_PANEL = "Blockchain Tracking";

        public void CreateRibbonPanel(UIControlledApplication application)
        {
            // Create custom ribbon tab
            application.CreateRibbonTab(RIBBON_TAB);

            // Create ribbon panel
            RibbonPanel ribbonPanel = application.CreateRibbonPanel(RIBBON_TAB, RIBBON_PANEL);

            // Add buttons
            AddStatusButton(ribbonPanel);
            AddSyncButton(ribbonPanel);
            ribbonPanel.AddSeparator();
            AddHistoryButton(ribbonPanel);
            AddAuditButton(ribbonPanel);
            ribbonPanel.AddSeparator();
            AddSettingsButton(ribbonPanel);
        }

        private void AddStatusButton(RibbonPanel panel)
        {
            PushButtonData buttonData = new PushButtonData(
                "BlockchainStatus",
                "Status",
                Assembly.GetExecutingAssembly().Location,
                "RevitBlockchain.Commands.BlockchainStatusCommand"
            )
            {
                ToolTip = "View blockchain connection status",
                LongDescription = "Shows the current connection status to the blockchain server and pending transactions.",
                LargeImage = LoadImage("status_32.png"),
                Image = LoadImage("status_16.png")
            };

            panel.AddItem(buttonData);
        }

        private void AddSyncButton(RibbonPanel panel)
        {
            PushButtonData buttonData = new PushButtonData(
                "BlockchainSync",
                "Force\nSync",
                Assembly.GetExecutingAssembly().Location,
                "RevitBlockchain.Commands.ForceSyncCommand"
            )
            {
                ToolTip = "Force blockchain synchronization",
                LongDescription = "Manually trigger synchronization of pending transactions with the blockchain server.",
                LargeImage = LoadImage("sync_32.png"),
                Image = LoadImage("sync_16.png")
            };

            panel.AddItem(buttonData);
        }

        private void AddHistoryButton(RibbonPanel panel)
        {
            PushButtonData buttonData = new PushButtonData(
                "ElementHistory",
                "Element\nHistory",
                Assembly.GetExecutingAssembly().Location,
                "RevitBlockchain.Commands.ElementHistoryCommand"
            )
            {
                ToolTip = "View element modification history",
                LongDescription = "Select an element to view its complete modification history from the blockchain.",
                LargeImage = LoadImage("history_32.png"),
                Image = LoadImage("history_16.png")
            };

            panel.AddItem(buttonData);
        }

        private void AddAuditButton(RibbonPanel panel)
        {
            PushButtonData buttonData = new PushButtonData(
                "AuditReport",
                "Audit\nReport",
                Assembly.GetExecutingAssembly().Location,
                "RevitBlockchain.Commands.AuditReportCommand"
            )
            {
                ToolTip = "Generate audit report",
                LongDescription = "Generate a comprehensive audit report of all changes tracked in the blockchain.",
                LargeImage = LoadImage("audit_32.png"),
                Image = LoadImage("audit_16.png")
            };

            panel.AddItem(buttonData);
        }

        private void AddSettingsButton(RibbonPanel panel)
        {
            PushButtonData buttonData = new PushButtonData(
                "BlockchainSettings",
                "Settings",
                Assembly.GetExecutingAssembly().Location,
                "RevitBlockchain.Commands.SettingsCommand"
            )
            {
                ToolTip = "Blockchain settings",
                LongDescription = "Configure blockchain server connection and other settings.",
                LargeImage = LoadImage("settings_32.png"),
                Image = LoadImage("settings_16.png")
            };

            panel.AddItem(buttonData);
        }

        private BitmapImage LoadImage(string imageName)
        {
            try
            {
                // Try to load from resources
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string imagePath = Path.Combine(Path.GetDirectoryName(assemblyPath), "Resources", imageName);

                if (File.Exists(imagePath))
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(imagePath);
                    image.EndInit();
                    return image;
                }
            }
            catch { }

            // Return default image if loading fails
            return GetDefaultImage(imageName.Contains("32"));
        }

        private BitmapImage GetDefaultImage(bool isLarge)
        {
            // Create a simple default image
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            
            // Use a built-in Revit icon as fallback
            string iconPath = isLarge 
                ? @"C:\Program Files\Autodesk\Revit 2024\AddIns\REX\System\Resources\RDBLink.ico"
                : @"C:\Program Files\Autodesk\Revit 2024\AddIns\REX\System\Resources\RDBLink.ico";
            
            if (File.Exists(iconPath))
            {
                image.UriSource = new Uri(iconPath);
            }
            
            image.EndInit();
            return image;
        }
    }
}
