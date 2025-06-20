using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using RevitBlockchain.Core;

namespace RevitBlockchain.RevitIntegration
{
    /// <summary>
    /// Handles synchronization events and creates blockchain transactions
    /// </summary>
    public class SyncEventHandler
    {
        private readonly BlockchainClient _blockchainClient;
        private Dictionary<string, object> _preSyncState;
        private DateTime _syncStartTime;

        public SyncEventHandler(BlockchainClient blockchainClient)
        {
            _blockchainClient = blockchainClient;
        }

        /// <summary>
        /// Called when sync with central starts
        /// </summary>
        public void OnSynchronizingWithCentral(object sender, SynchronizingWithCentralEventArgs args)
        {
            _syncStartTime = DateTime.UtcNow;
            var doc = args.Document;

            // Capture pre-sync state
            _preSyncState = CaptureDocumentState(doc);

            // Create sync initiation transaction
            var transaction = new Transaction
            {
                Id = GenerateTransactionId(),
                Type = "SyncInitiated",
                Data = new Dictionary<string, object>
                {
                    ["documentTitle"] = doc.Title,
                    ["documentGuid"] = doc.GetCloudModelPath()?.GetModelGUID()?.ToString() ?? "local",
                    ["user"] = doc.Application.Username,
                    ["syncOptions"] = SerializeSyncOptions(args.Options),
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
                    ["workstation"] = Environment.MachineName
                }
            };

            Task.Run(async () => await _blockchainClient.SubmitTransaction(transaction));
        }

        /// <summary>
        /// Called when sync with central completes
        /// </summary>
        public void OnSynchronizedWithCentral(object sender, DocumentSynchronizedWithCentralEventArgs args)
        {
            var doc = args.Document;
            var syncDuration = DateTime.UtcNow - _syncStartTime;

            // Capture post-sync state
            var postSyncState = CaptureDocumentState(doc);
            
            // Calculate changes
            var changes = CalculateChanges(_preSyncState, postSyncState);
            
            // Get modified elements
            var modifiedElements = GetModifiedElements(doc);
            
            // Create sync completion transaction
            var transaction = new Transaction
            {
                Id = GenerateTransactionId(),
                Type = "SyncCompleted",
                Data = new Dictionary<string, object>
                {
                    ["documentTitle"] = doc.Title,
                    ["documentGuid"] = doc.GetCloudModelPath()?.GetModelGUID()?.ToString() ?? "local",
                    ["user"] = doc.Application.Username,
                    ["syncDuration"] = syncDuration.TotalSeconds,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
                    ["changes"] = changes,
                    ["modifiedElements"] = modifiedElements.Select(e => new
                    {
                        id = e.Id.IntegerValue,
                        category = e.Category?.Name,
                        name = e.Name
                    }).ToList(),
                    ["worksetChanges"] = GetWorksetChanges(doc)
                }
            };

            Task.Run(async () => 
            {
                // Submit main transaction
                var result = await _blockchainClient.SubmitTransaction(transaction);
                
                // If successful, also record element-level changes
                if (result.Success)
                {
                    await RecordElementChanges(doc, modifiedElements);
                }
                
                // Call smart contract to update workset ownership
                await UpdateWorksetOwnership(doc);
            });
        }

        /// <summary>
        /// Capture current document state for comparison
        /// </summary>
        private Dictionary<string, object> CaptureDocumentState(Document doc)
        {
            var state = new Dictionary<string, object>();
            
            // Capture workset information
            if (doc.IsWorkshared)
            {
                var worksets = new FilteredWorksetCollector(doc)
                    .OfKind(WorksetKind.UserWorkset)
                    .Cast<Workset>()
                    .Select(w => new
                    {
                        id = w.Id.IntegerValue,
                        name = w.Name,
                        owner = w.Owner,
                        isEditable = w.IsEditable
                    })
                    .ToList();
                    
                state["worksets"] = worksets;
            }
            
            // Capture element counts by category
            var elementCounts = new Dictionary<string, int>();
            foreach (Category cat in doc.Settings.Categories)
            {
                if (cat.AllowsBoundParameters && cat.CategoryType == CategoryType.Model)
                {
                    var count = new FilteredElementCollector(doc)
                        .OfCategoryId(cat.Id)
                        .WhereElementIsNotElementType()
                        .GetElementCount();
                    
                    if (count > 0)
                        elementCounts[cat.Name] = count;
                }
            }
            state["elementCounts"] = elementCounts;
            
            return state;
        }

        /// <summary>
        /// Calculate what changed between two states
        /// </summary>
        private Dictionary<string, object> CalculateChanges(
            Dictionary<string, object> preState, 
            Dictionary<string, object> postState)
        {
            var changes = new Dictionary<string, object>();
            
            // Compare element counts
            var preCounts = preState["elementCounts"] as Dictionary<string, int>;
            var postCounts = postState["elementCounts"] as Dictionary<string, int>;
            
            var addedElements = new Dictionary<string, int>();
            var deletedElements = new Dictionary<string, int>();
            var modifiedCategories = new List<string>();
            
            // Find additions and modifications
            foreach (var kvp in postCounts)
            {
                if (!preCounts.ContainsKey(kvp.Key))
                {
                    addedElements[kvp.Key] = kvp.Value;
                }
                else if (preCounts[kvp.Key] != kvp.Value)
                {
                    var diff = kvp.Value - preCounts[kvp.Key];
                    if (diff > 0)
                        addedElements[kvp.Key] = diff;
                    else
                        deletedElements[kvp.Key] = -diff;
                }
            }
            
            // Find deletions
            foreach (var kvp in preCounts)
            {
                if (!postCounts.ContainsKey(kvp.Key))
                {
                    deletedElements[kvp.Key] = kvp.Value;
                }
            }
            
            changes["addedElements"] = addedElements;
            changes["deletedElements"] = deletedElements;
            changes["totalAdded"] = addedElements.Values.Sum();
            changes["totalDeleted"] = deletedElements.Values.Sum();
            
            return changes;
        }

        /// <summary>
        /// Get all elements modified in this sync
        /// </summary>
        private List<Element> GetModifiedElements(Document doc)
        {
            var modifiedElements = new List<Element>();
            
            // Get editing session ID to track changes
            var editingSession = WorksharingUtils.GetActiveEditingSession(doc);
            
            // This is a simplified version - in production, you'd track element changes
            // more precisely using the DocumentChanged event
            
            return modifiedElements;
        }

        /// <summary>
        /// Get workset ownership changes
        /// </summary>
        private List<object> GetWorksetChanges(Document doc)
        {
            var changes = new List<object>();
            
            if (doc.IsWorkshared)
            {
                var worksets = new FilteredWorksetCollector(doc)
                    .OfKind(WorksetKind.UserWorkset)
                    .Cast<Workset>();
                    
                foreach (var workset in worksets)
                {
                    changes.Add(new
                    {
                        worksetId = workset.Id.IntegerValue,
                        worksetName = workset.Name,
                        owner = workset.Owner,
                        isEditable = workset.IsEditable
                    });
                }
            }
            
            return changes;
        }

        /// <summary>
        /// Record individual element changes to blockchain
        /// </summary>
        private async Task RecordElementChanges(Document doc, List<Element> elements)
        {
            foreach (var element in elements)
            {
                var elementData = new Dictionary<string, object>
                {
                    ["elementId"] = element.Id.IntegerValue,
                    ["uniqueId"] = element.UniqueId,
                    ["category"] = element.Category?.Name,
                    ["name"] = element.Name,
                    ["level"] = GetElementLevel(element),
                    ["workset"] = GetElementWorkset(doc, element),
                    ["parameters"] = GetKeyParameters(element)
                };

                // Calculate element hash
                var elementHash = CryptoHelper.CalculateElementHash(element);
                
                // Call smart contract to record modification
                await _blockchainClient.CallContract(
                    "ElementModificationContract",
                    "record_modification",
                    new
                    {
                        element_id = element.UniqueId,
                        old_hash = "previous_hash", // Would track this properly
                        new_hash = elementHash,
                        user_id = doc.Application.Username,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
                        metadata = elementData
                    }
                );
            }
        }

        /// <summary>
        /// Update workset ownership in smart contract
        /// </summary>
        private async Task UpdateWorksetOwnership(Document doc)
        {
            if (!doc.IsWorkshared) return;
            
            var worksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .Cast<Workset>();
                
            foreach (var workset in worksets)
            {
                await _blockchainClient.CallContract(
                    "WorksetOwnershipContract",
                    "update_ownership",
                    new
                    {
                        workset_id = workset.Id.IntegerValue,
                        workset_name = workset.Name,
                        owner = workset.Owner,
                        is_editable = workset.IsEditable
                    }
                );
            }
        }

        private string GetElementLevel(Element element)
        {
            var levelParam = element.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
            if (levelParam != null && levelParam.HasValue)
            {
                var levelId = levelParam.AsElementId();
                var level = element.Document.GetElement(levelId) as Level;
                return level?.Name ?? "Unknown";
            }
            return "N/A";
        }

        private string GetElementWorkset(Document doc, Element element)
        {
            if (doc.IsWorkshared)
            {
                var worksetId = element.WorksetId;
                var workset = doc.GetWorksetTable().GetWorkset(worksetId);
                return workset?.Name ?? "Unknown";
            }
            return "N/A";
        }

        private Dictionary<string, object> GetKeyParameters(Element element)
        {
            var parameters = new Dictionary<string, object>();
            
            // Get key parameters that are often tracked
            var trackedParams = new[]
            {
                BuiltInParameter.ALL_MODEL_MARK,
                BuiltInParameter.ALL_MODEL_TYPE_COMMENTS,
                BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS
            };
            
            foreach (var paramId in trackedParams)
            {
                var param = element.get_Parameter(paramId);
                if (param != null && param.HasValue)
                {
                    parameters[param.Definition.Name] = param.AsValueString();
                }
            }
            
            return parameters;
        }

        private Dictionary<string, object> SerializeSyncOptions(SynchronizeWithCentralOptions options)
        {
            return new Dictionary<string, object>
            {
                ["compact"] = options.Compact,
                ["saveLocalBefore"] = options.SaveLocalBefore,
                ["saveLocalAfter"] = options.SaveLocalAfter,
                ["comment"] = options.Comment
            };
        }

        private string GenerateTransactionId()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
            var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"{timestamp}-Revit-{guid}";
        }
    }
}
