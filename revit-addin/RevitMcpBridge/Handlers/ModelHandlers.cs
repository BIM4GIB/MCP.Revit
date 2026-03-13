using Autodesk.Revit.DB;
using System.Linq;

namespace RevitMcpBridge.Handlers
{
    public static class ModelHandlers
    {
        public static object GetModelInfo(Document doc)
        {
            var info = doc.ProjectInformation;
            return new
            {
                title            = doc.Title,
                filePath         = doc.PathName,
                revitVersion     = doc.Application.VersionNumber,
                elementCount     = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType().GetElementCount(),
                projectName      = info?.Name ?? "",
                projectNumber    = info?.Number ?? "",
                projectStatus    = info?.Status ?? "",
                buildingName     = info?.BuildingName ?? "",
                author           = info?.Author ?? "",
                organizationName = info?.OrganizationName ?? "",
            };
        }

        public static object GetCategories(Document doc)
        {
            var categories = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements()
                .Select(e => e.Category?.Name)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return new { categories };
        }

        public static object GetLevels(Document doc)
        {
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .Select(l => new
                {
                    name      = l.Name,
                    elevation = UnitUtils.ConvertFromInternalUnits(
                        l.Elevation, UnitTypeId.Feet),
                })
                .ToList();

            return new { levels };
        }
    }
}
