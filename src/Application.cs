using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using RevitBlockchain.Core;
using RevitBlockchain.RevitIntegration;
using RevitBlockchain.UI;

namespace RevitBlockchain
{
    /// <summary>
    /// Main application class for RevitBlockchain add-in
    /// </summary>
    public class Application : IExternalApplication
    {
        private static BlockchainClient _blockchainClient;
        private static EventHandlerManager _eventManager;
        private static BlockchainRibbon _ribbon;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Initialize blockchain client
                _blockchainClient = new BlockchainClient();
                
                // Initialize event handler manager
                _eventManager = new EventHandlerManager(_blockchainClient);
                
                // Create ribbon UI
                _ribbon = new BlockchainRibbon();
                _ribbon.CreateRibbonPanel(application);
                
                // Register global event handlers
                application.ControlledApplication.DocumentOpened += OnDocumentOpened;
                application.ControlledApplication.DocumentClosing += OnDocumentClosing;
                
                // Log startup
                _blockchainClient.LogEvent("RevitBlockchain", "Startup", "Add-in initialized successfully");
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RevitBlockchain Error", $"Failed to initialize: {ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                // Cleanup event handlers
                application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
                application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
                
                // Disconnect from blockchain
                _blockchainClient?.Disconnect();
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RevitBlockchain Error", $"Error during shutdown: {ex.Message}");
                return Result.Failed;
            }
        }

        private void OnDocumentOpened(object sender, DocumentOpenedEventArgs args)
        {
            var doc = args.Document;
            if (doc.IsWorkshared)
            {
                // Register document-specific event handlers
                _eventManager.RegisterDocumentEvents(doc);
                
                // Initialize workset tracking
                var worksetTracker = new WorksetTracker(doc, _blockchainClient);
                worksetTracker.Initialize();
                
                // Log document opened
                _blockchainClient.LogEvent(
                    "DocumentOpened",
                    doc.Title,
                    $"User: {doc.Application.Username}, Path: {doc.PathName}"
                );
            }
        }

        private void OnDocumentClosing(object sender, DocumentClosingEventArgs args)
        {
            var doc = args.Document;
            if (doc.IsWorkshared)
            {
                // Unregister document events
                _eventManager.UnregisterDocumentEvents(doc);
                
                // Log document closing
                _blockchainClient.LogEvent(
                    "DocumentClosing",
                    doc.Title,
                    $"User: {doc.Application.Username}"
                );
            }
        }

        public static BlockchainClient GetBlockchainClient()
        {
            return _blockchainClient;
        }
    }

    /// <summary>
    /// Manages all Revit event handlers for blockchain tracking
    /// </summary>
    public class EventHandlerManager
    {
        private readonly BlockchainClient _blockchainClient;
        private readonly SyncEventHandler _syncHandler;
        private readonly ElementChangeHandler _elementChangeHandler;

        public EventHandlerManager(BlockchainClient blockchainClient)
        {
            _blockchainClient = blockchainClient;
            _syncHandler = new SyncEventHandler(blockchainClient);
            _elementChangeHandler = new ElementChangeHandler(blockchainClient);
        }

        public void RegisterDocumentEvents(Document doc)
        {
            // Sync events
            doc.SynchronizingWithCentral += _syncHandler.OnSynchronizingWithCentral;
            doc.SynchronizedWithCentral += _syncHandler.OnSynchronizedWithCentral;
            
            // Change tracking
            doc.Application.DocumentChanged += _elementChangeHandler.OnDocumentChanged;
            
            // Workset events
            doc.WorksharingOperationProgressChanged += OnWorksharingOperationProgressChanged;
        }

        public void UnregisterDocumentEvents(Document doc)
        {
            // Remove all event handlers
            doc.SynchronizingWithCentral -= _syncHandler.OnSynchronizingWithCentral;
            doc.SynchronizedWithCentral -= _syncHandler.OnSynchronizedWithCentral;
            doc.Application.DocumentChanged -= _elementChangeHandler.OnDocumentChanged;
            doc.WorksharingOperationProgressChanged -= OnWorksharingOperationProgressChanged;
        }

        private void OnWorksharingOperationProgressChanged(object sender, WorksharingOperationProgressChangedEventArgs args)
        {
            // Track worksharing operations
            _blockchainClient.LogEvent(
                "WorksharingOperation",
                args.Operation.ToString(),
                $"Progress: {args.ProgressValue}/{args.ProgressMax}"
            );
        }
    }
}
