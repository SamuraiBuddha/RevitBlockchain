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
    /// Tracks element changes and prepares them for blockchain recording
    /// </summary>
    public class ElementChangeHandler
    {
        private readonly BlockchainClient _blockchainClient;
        private readonly Dictionary<string, ElementChangeInfo> _pendingChanges;
        private readonly object _lockObject = new object();

        public ElementChangeHandler(BlockchainClient blockchainClient)
        {
            _blockchainClient = blockchainClient;
            _pendingChanges = new Dictionary<string, ElementChangeInfo>();
        }

        /// <summary>
        /// Handle document changed event
        /// </summary>
        public void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            var doc = args.GetDocument();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;

            lock (_lockObject)
            {
                // Track added elements
                foreach (var elementId in args.GetAddedElementIds())
                {
                    var element = doc.GetElement(elementId);
                    if (ShouldTrackElement(element))
                    {
                        RecordElementChange(element, ChangeType.Added, timestamp);
                    }
                }

                // Track modified elements
                foreach (var elementId in args.GetModifiedElementIds())
                {
                    var element = doc.GetElement(elementId);
                    if (ShouldTrackElement(element))
                    {
                        RecordElementChange(element, ChangeType.Modified, timestamp);
                    }
                }

                // Track deleted elements
                foreach (var elementId in args.GetDeletedElementIds())
                {
                    RecordElementDeletion(elementId, timestamp, doc);
                }
            }

            // Log transaction operation if this is a transaction end
            var transactionNames = args.GetTransactionNames();
            if (transactionNames.Count > 0)
            {
                LogTransactionOperation(doc, transactionNames, args);
            }
        }

        /// <summary>
        /// Get all pending changes (called during sync)
        /// </summary>
        public List<ElementChangeInfo> GetPendingChanges()
        {
            lock (_lockObject)
            {
                var changes = _pendingChanges.Values.ToList();
                _pendingChanges.Clear();
                return changes;
            }
        }

        /// <summary>
        /// Determine if an element should be tracked
        /// </summary>
        private bool ShouldTrackElement(Element element)
        {
            if (element == null) return false;

            // Skip view-specific elements
            if (element.ViewSpecific) return false;

            // Skip temporary elements
            if (element.IsTemporary) return false;

            // Track model elements
            if (element.Category != null && 
                element.Category.CategoryType == CategoryType.Model)
            {
                return true;
            }

            // Track specific non-model elements
            var trackedTypes = new[]
            {
                typeof(Material),
                typeof(FamilySymbol),
                typeof(Level),
                typeof(Grid),
                typeof(ReferencePlane)
            };

            return trackedTypes.Any(t => t.IsInstanceOfType(element));
        }

        /// <summary>
        /// Record an element change
        /// </summary>
        private void RecordElementChange(Element element, ChangeType changeType, long timestamp)
        {
            var uniqueId = element.UniqueId;
            var elementInfo = new ElementChangeInfo
            {
                ElementId = element.Id.IntegerValue,
                UniqueId = uniqueId,
                Category = element.Category?.Name ?? "Unknown",
                ElementName = element.Name,
                ChangeType = changeType,
                Timestamp = timestamp,
                User = element.Document.Application.Username,
                WorksetName = GetWorksetName(element)
            };

            // Capture element state
            elementInfo.ElementData = CaptureElementState(element);
            elementInfo.Hash = CryptoHelper.CalculateElementHash(element);

            // Store or update pending change
            _pendingChanges[uniqueId] = elementInfo;
        }

        /// <summary>
        /// Record element deletion
        /// </summary>
        private void RecordElementDeletion(ElementId elementId, long timestamp, Document doc)
        {
            var deletionInfo = new ElementChangeInfo
            {
                ElementId = elementId.IntegerValue,
                UniqueId = $"deleted_{elementId.IntegerValue}",
                Category = "Unknown",
                ElementName = "Deleted Element",
                ChangeType = ChangeType.Deleted,
                Timestamp = timestamp,
                User = doc.Application.Username
            };

            _pendingChanges[deletionInfo.UniqueId] = deletionInfo;
        }

        /// <summary>
        /// Capture current state of an element
        /// </summary>
        private Dictionary<string, object> CaptureElementState(Element element)
        {
            var state = new Dictionary<string, object>
            {
                ["id"] = element.Id.IntegerValue,
                ["uniqueId"] = element.UniqueId,
                ["category"] = element.Category?.Name,
                ["name"] = element.Name,
                ["type"] = element.GetType().Name
            };

            // Capture geometry if applicable
            if (element.Location != null)
            {
                state["location"] = SerializeLocation(element.Location);
            }

            // Capture key parameters
            var parameters = new Dictionary<string, object>();
            foreach (Parameter param in element.Parameters)
            {
                if (param.HasValue && !param.IsReadOnly)
                {
                    parameters[param.Definition.Name] = GetParameterValue(param);
                }
            }
            state["parameters"] = parameters;

            // Capture bounding box
            var bbox = element.get_BoundingBox(null);
            if (bbox != null)
            {
                state["boundingBox"] = new
                {
                    min = new { x = bbox.Min.X, y = bbox.Min.Y, z = bbox.Min.Z },
                    max = new { x = bbox.Max.X, y = bbox.Max.Y, z = bbox.Max.Z }
                };
            }

            return state;
        }

        /// <summary>
        /// Log transaction operation to blockchain
        /// </summary>
        private void LogTransactionOperation(Document doc, IList<string> transactionNames, DocumentChangedEventArgs args)
        {
            var transaction = new Transaction
            {
                Id = GenerateTransactionId(),
                Type = "RevitTransaction",
                Data = new Dictionary<string, object>
                {
                    ["transactionNames"] = transactionNames,
                    ["addedCount"] = args.GetAddedElementIds().Count,
                    ["modifiedCount"] = args.GetModifiedElementIds().Count,
                    ["deletedCount"] = args.GetDeletedElementIds().Count,
                    ["user"] = doc.Application.Username,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
                    ["documentTitle"] = doc.Title
                }
            };

            Task.Run(async () => await _blockchainClient.SubmitTransaction(transaction));
        }

        /// <summary>
        /// Get workset name for element
        /// </summary>
        private string GetWorksetName(Element element)
        {
            if (element.Document.IsWorkshared && element.WorksetId != null)
            {
                var workset = element.Document.GetWorksetTable().GetWorkset(element.WorksetId);
                return workset?.Name ?? "Default";
            }
            return "N/A";
        }

        /// <summary>
        /// Serialize element location
        /// </summary>
        private object SerializeLocation(Location location)
        {
            if (location is LocationPoint locPoint)
            {
                var point = locPoint.Point;
                return new { type = "point", x = point.X, y = point.Y, z = point.Z };
            }
            else if (location is LocationCurve locCurve)
            {
                var curve = locCurve.Curve;
                var start = curve.GetEndPoint(0);
                var end = curve.GetEndPoint(1);
                return new
                {
                    type = "curve",
                    start = new { x = start.X, y = start.Y, z = start.Z },
                    end = new { x = end.X, y = end.Y, z = end.Z }
                };
            }
            return null;
        }

        /// <summary>
        /// Get parameter value as object
        /// </summary>
        private object GetParameterValue(Parameter param)
        {
            switch (param.StorageType)
            {
                case StorageType.Double:
                    return param.AsDouble();
                case StorageType.Integer:
                    return param.AsInteger();
                case StorageType.String:
                    return param.AsString();
                case StorageType.ElementId:
                    var id = param.AsElementId();
                    if (id.IntegerValue > 0)
                    {
                        var elem = param.Element.Document.GetElement(id);
                        return elem?.Name ?? id.IntegerValue.ToString();
                    }
                    return null;
                default:
                    return param.AsValueString();
            }
        }

        private string GenerateTransactionId()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
            var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"{timestamp}-Change-{guid}";
        }
    }

    /// <summary>
    /// Information about an element change
    /// </summary>
    public class ElementChangeInfo
    {
        public int ElementId { get; set; }
        public string UniqueId { get; set; }
        public string Category { get; set; }
        public string ElementName { get; set; }
        public ChangeType ChangeType { get; set; }
        public long Timestamp { get; set; }
        public string User { get; set; }
        public string WorksetName { get; set; }
        public Dictionary<string, object> ElementData { get; set; }
        public string Hash { get; set; }
    }

    public enum ChangeType
    {
        Added,
        Modified,
        Deleted
    }
}
