using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMcpBridge.Handlers
{
    public static class ElementHandlers
    {
        public static object QueryElements(Document doc, JObject query)
        {
            string? category   = query["category"]?.Value<string>();
            string? familyName = query["familyName"]?.Value<string>();
            string? typeName   = query["typeName"]?.Value<string>();
            string? levelName  = query["levelName"]?.Value<string>();
            int limit          = query["limit"]?.Value<int>() ?? 50;
            var paramFilters   = query["parameterFilters"] as JArray;

            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            if (!string.IsNullOrEmpty(category))
            {
                var bic = GetBuiltInCategory(category);
                if (bic != BuiltInCategory.INVALID)
                    collector = collector.WherePasses(new ElementCategoryFilter(bic));
            }

            var elements = collector.ToElements()
                .Where(e => e.Category != null)
                .Where(e => string.IsNullOrEmpty(familyName) ||
                    (e is FamilyInstance fi &&
                     fi.Symbol.Family.Name.IndexOf(familyName, StringComparison.OrdinalIgnoreCase) >= 0))
                .Where(e =>
                {
                    if (string.IsNullOrEmpty(typeName)) return true;
                    var typeEl = doc.GetElement(e.GetTypeId()) as ElementType;
                    return typeEl?.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .Where(e =>
                {
                    if (string.IsNullOrEmpty(levelName)) return true;
                    var lvlId = e.LevelId;
                    if (lvlId == ElementId.InvalidElementId) return false;
                    var lvl = doc.GetElement(lvlId) as Level;
                    return lvl?.Name.IndexOf(levelName, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .Where(e => MatchesParamFilters(e, paramFilters))
                .Take(limit)
                .Select(e => SerializeElement(doc, e))
                .ToList();

            return new { elements };
        }

        public static object? GetElementById(Document doc, long id)
        {
            var element = doc.GetElement(ElementIdHelper.Create(id));
            return element == null ? null : SerializeElement(doc, element);
        }

        private static object SerializeElement(Document doc, Element e)
        {
            var parameters = new Dictionary<string, object?>();
            foreach (Parameter p in e.Parameters)
            {
                if (p.Definition == null) continue;
                parameters[p.Definition.Name] = GetParameterValue(p);
            }

            Level? level = e.LevelId != ElementId.InvalidElementId
                ? doc.GetElement(e.LevelId) as Level
                : null;

            XYZ? locationPoint = (e.Location as LocationPoint)?.Point;
            var fi = e as FamilyInstance;
            var typeEl = doc.GetElement(e.GetTypeId()) as ElementType;

            return new
            {
                id         = ElementIdHelper.GetValue(e.Id),
                uniqueId   = e.UniqueId,
                category   = e.Category?.Name ?? "",
                familyName = fi?.Symbol.Family.Name ?? "",
                typeName   = typeEl?.Name ?? "",
                levelName  = level?.Name ?? "",
                parameters,
                location   = locationPoint == null ? null : new
                {
                    x = Math.Round(locationPoint.X, 4),
                    y = Math.Round(locationPoint.Y, 4),
                    z = Math.Round(locationPoint.Z, 4),
                },
            };
        }

        private static object? GetParameterValue(Parameter p)
        {
            if (!p.HasValue) return null;
            return p.StorageType switch
            {
                StorageType.String    => p.AsString(),
                StorageType.Integer   => p.AsInteger(),
                StorageType.Double    => Math.Round(p.AsDouble(), 6),
                StorageType.ElementId => p.AsElementId() is ElementId eid
                    ? ElementIdHelper.GetValue(eid) : null,
                _                     => null,
            };
        }

        private static bool MatchesParamFilters(Element e, JArray? filters)
        {
            if (filters == null || !filters.HasValues) return true;

            foreach (var f in filters)
            {
                string paramName = f["parameterName"]?.Value<string>() ?? "";
                string op        = f["operator"]?.Value<string>() ?? "equals";
                var filterValue  = f["value"];

                var param = e.LookupParameter(paramName);
                if (param == null) return false;

                var value = GetParameterValue(param);
                if (value == null) return false;

                bool match = op switch
                {
                    "equals"      => value.ToString()!.Equals(
                        filterValue?.ToString(), StringComparison.OrdinalIgnoreCase),
                    "contains"    => value.ToString()!.Contains(
                        filterValue?.ToString() ?? "", StringComparison.OrdinalIgnoreCase),
                    "startsWith"  => value.ToString()!.StartsWith(
                        filterValue?.ToString() ?? "", StringComparison.OrdinalIgnoreCase),
                    "greaterThan" => double.TryParse(value.ToString(), out var d1) &&
                        double.TryParse(filterValue?.ToString(), out var d2) && d1 > d2,
                    "lessThan"    => double.TryParse(value.ToString(), out var d3) &&
                        double.TryParse(filterValue?.ToString(), out var d4) && d3 < d4,
                    _             => false,
                };

                if (!match) return false;
            }
            return true;
        }

        private static BuiltInCategory GetBuiltInCategory(string name)
        {
            var map = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
            {
                ["Walls"]                  = BuiltInCategory.OST_Walls,
                ["Floors"]                 = BuiltInCategory.OST_Floors,
                ["Ceilings"]               = BuiltInCategory.OST_Ceilings,
                ["Roofs"]                  = BuiltInCategory.OST_Roofs,
                ["Doors"]                  = BuiltInCategory.OST_Doors,
                ["Windows"]                = BuiltInCategory.OST_Windows,
                ["Rooms"]                  = BuiltInCategory.OST_Rooms,
                ["Columns"]                = BuiltInCategory.OST_Columns,
                ["Structural Columns"]     = BuiltInCategory.OST_StructuralColumns,
                ["Structural Framing"]     = BuiltInCategory.OST_StructuralFraming,
                ["Beams"]                  = BuiltInCategory.OST_StructuralFraming,
                ["Furniture"]              = BuiltInCategory.OST_Furniture,
                ["Mechanical Equipment"]   = BuiltInCategory.OST_MechanicalEquipment,
                ["Plumbing Fixtures"]      = BuiltInCategory.OST_PlumbingFixtures,
                ["Electrical Fixtures"]    = BuiltInCategory.OST_ElectricalFixtures,
                ["Electrical Equipment"]   = BuiltInCategory.OST_ElectricalEquipment,
                ["Grids"]                  = BuiltInCategory.OST_Grids,
                ["Levels"]                 = BuiltInCategory.OST_Levels,
                ["Views"]                  = BuiltInCategory.OST_Views,
                ["Sheets"]                 = BuiltInCategory.OST_Sheets,
                ["Stairs"]                 = BuiltInCategory.OST_Stairs,
                ["Railings"]               = BuiltInCategory.OST_StairsRailing,
                ["Curtain Walls"]          = BuiltInCategory.OST_CurtainWallPanels,
                ["Curtain Wall Mullions"]  = BuiltInCategory.OST_CurtainWallMullions,
                ["Generic Models"]         = BuiltInCategory.OST_GenericModel,
                ["Pipes"]                  = BuiltInCategory.OST_PipeCurves,
                ["Ducts"]                  = BuiltInCategory.OST_DuctCurves,
                ["Cable Trays"]            = BuiltInCategory.OST_CableTray,
                ["Conduits"]               = BuiltInCategory.OST_Conduit,
                ["Structural Foundations"] = BuiltInCategory.OST_StructuralFoundation,
                ["Parking"]                = BuiltInCategory.OST_Parking,
                ["Areas"]                  = BuiltInCategory.OST_Areas,
                ["Mass"]                   = BuiltInCategory.OST_Mass,
                ["Topography"]             = BuiltInCategory.OST_Topography,
            };

            return map.TryGetValue(name, out var bic) ? bic : BuiltInCategory.INVALID;
        }
    }
}
