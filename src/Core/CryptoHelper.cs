using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Newtonsoft.Json;

namespace RevitBlockchain.Core
{
    /// <summary>
    /// Cryptographic utilities for blockchain operations
    /// </summary>
    public static class CryptoHelper
    {
        /// <summary>
        /// Calculate hash for a Revit element
        /// </summary>
        public static string CalculateElementHash(Element element)
        {
            if (element == null) return "null";

            var hashData = new Dictionary<string, object>
            {
                ["uniqueId"] = element.UniqueId,
                ["category"] = element.Category?.Name ?? "none",
                ["elementType"] = element.GetType().Name,
                ["name"] = element.Name ?? string.Empty
            };

            // Add geometry hash if applicable
            var geoHash = CalculateGeometryHash(element);
            if (!string.IsNullOrEmpty(geoHash))
            {
                hashData["geometry"] = geoHash;
            }

            // Add parameter values
            var paramHash = CalculateParameterHash(element);
            hashData["parameters"] = paramHash;

            // Add location
            var locHash = CalculateLocationHash(element);
            if (!string.IsNullOrEmpty(locHash))
            {
                hashData["location"] = locHash;
            }

            // Serialize and hash
            var json = JsonConvert.SerializeObject(hashData, Formatting.None);
            return CalculateSHA256(json);
        }

        /// <summary>
        /// Calculate hash for element geometry
        /// </summary>
        private static string CalculateGeometryHash(Element element)
        {
            try
            {
                var options = new Options
                {
                    ComputeReferences = false,
                    IncludeNonVisibleObjects = false
                };

                var geometry = element.get_Geometry(options);
                if (geometry == null) return string.Empty;

                var vertices = new List<string>();
                foreach (GeometryObject geoObj in geometry)
                {
                    if (geoObj is Solid solid)
                    {
                        foreach (Edge edge in solid.Edges)
                        {
                            var curve = edge.AsCurve();
                            if (curve != null)
                            {
                                var start = curve.GetEndPoint(0);
                                var end = curve.GetEndPoint(1);
                                vertices.Add($"{start.X:F6},{start.Y:F6},{start.Z:F6}");
                                vertices.Add($"{end.X:F6},{end.Y:F6},{end.Z:F6}");
                            }
                        }
                    }
                }

                if (vertices.Count > 0)
                {
                    vertices.Sort(); // Ensure consistent ordering
                    return CalculateSHA256(string.Join(";", vertices));
                }
            }
            catch
            {
                // Geometry calculation can fail for some elements
            }

            return string.Empty;
        }

        /// <summary>
        /// Calculate hash for element parameters
        /// </summary>
        private static string CalculateParameterHash(Element element)
        {
            var paramData = new SortedDictionary<string, string>(); // Sorted for consistency

            foreach (Parameter param in element.Parameters)
            {
                if (!param.HasValue || param.IsReadOnly) continue;

                var value = GetParameterValueAsString(param);
                if (!string.IsNullOrEmpty(value))
                {
                    paramData[param.Definition.Name] = value;
                }
            }

            var json = JsonConvert.SerializeObject(paramData, Formatting.None);
            return CalculateSHA256(json);
        }

        /// <summary>
        /// Calculate hash for element location
        /// </summary>
        private static string CalculateLocationHash(Element element)
        {
            var location = element.Location;
            if (location == null) return string.Empty;

            if (location is LocationPoint locPoint)
            {
                var point = locPoint.Point;
                return CalculateSHA256($"point:{point.X:F6},{point.Y:F6},{point.Z:F6}");
            }
            else if (location is LocationCurve locCurve)
            {
                var curve = locCurve.Curve;
                var start = curve.GetEndPoint(0);
                var end = curve.GetEndPoint(1);
                return CalculateSHA256($"curve:{start.X:F6},{start.Y:F6},{start.Z:F6}-{end.X:F6},{end.Y:F6},{end.Z:F6}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Get parameter value as string for hashing
        /// </summary>
        private static string GetParameterValueAsString(Parameter param)
        {
            try
            {
                switch (param.StorageType)
                {
                    case StorageType.Double:
                        return param.AsDouble().ToString("F6");
                    case StorageType.Integer:
                        return param.AsInteger().ToString();
                    case StorageType.String:
                        return param.AsString() ?? string.Empty;
                    case StorageType.ElementId:
                        return param.AsElementId().IntegerValue.ToString();
                    default:
                        return param.AsValueString() ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Calculate SHA256 hash of a string
        /// </summary>
        public static string CalculateSHA256(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// Calculate hash for a transaction
        /// </summary>
        public static string CalculateTransactionHash(Transaction transaction)
        {
            var data = JsonConvert.SerializeObject(new
            {
                id = transaction.Id,
                type = transaction.Type,
                data = transaction.Data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }, Formatting.None);

            return CalculateSHA256(data);
        }

        /// <summary>
        /// Generate a unique identifier
        /// </summary>
        public static string GenerateUniqueId()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000; // Microseconds
            var random = new Random().Next(100000, 999999);
            var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"{timestamp}-{random}-{guid}";
        }

        /// <summary>
        /// Verify element hash
        /// </summary>
        public static bool VerifyElementHash(Element element, string expectedHash)
        {
            var currentHash = CalculateElementHash(element);
            return currentHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Calculate Merkle root for a list of hashes
        /// </summary>
        public static string CalculateMerkleRoot(List<string> hashes)
        {
            if (hashes == null || hashes.Count == 0) return string.Empty;
            if (hashes.Count == 1) return hashes[0];

            var workingHashes = new List<string>(hashes);

            while (workingHashes.Count > 1)
            {
                var newLevel = new List<string>();

                for (int i = 0; i < workingHashes.Count; i += 2)
                {
                    if (i + 1 < workingHashes.Count)
                    {
                        var combined = workingHashes[i] + workingHashes[i + 1];
                        newLevel.Add(CalculateSHA256(combined));
                    }
                    else
                    {
                        // Odd number of hashes, duplicate the last one
                        var combined = workingHashes[i] + workingHashes[i];
                        newLevel.Add(CalculateSHA256(combined));
                    }
                }

                workingHashes = newLevel;
            }

            return workingHashes[0];
        }

        /// <summary>
        /// Sign data with a private key (placeholder - would use real crypto in production)
        /// </summary>
        public static string SignData(string data, string privateKey)
        {
            // In production, this would use RSA or ECDSA
            // For now, just create a hash with the private key
            var combined = data + privateKey;
            return CalculateSHA256(combined);
        }

        /// <summary>
        /// Verify signature (placeholder - would use real crypto in production)
        /// </summary>
        public static bool VerifySignature(string data, string signature, string publicKey)
        {
            // In production, this would use RSA or ECDSA verification
            // For now, just recreate the hash
            var expectedSignature = SignData(data, publicKey);
            return signature.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase);
        }
    }
}
