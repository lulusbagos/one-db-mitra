using System.Collections.Generic;

namespace one_db_mitra.Models.Menu
{
    public class MenuScope
    {
        public int CompanyId { get; init; }
        public int RoleId { get; init; }
        public IReadOnlyList<int> RoleIds { get; init; } = Array.Empty<int>();
        public int? DepartmentId { get; init; }
        public int? SectionId { get; init; }
        public int? PositionId { get; init; }
    }
}
