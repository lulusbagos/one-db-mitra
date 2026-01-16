using System;
using System.Collections.Generic;
using System.Linq;

namespace one_db_mitra.Models.Menu
{
    /// <summary>
    /// Representasi node menu hirarkis yang bisa dipakai untuk sidebar maupun shortcut startup.
    /// </summary>
    public class MenuItem
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string MenuCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string IconKey { get; set; } = "bi-circle";
        public string UrlPath { get; set; } = "#";
        public bool IsHidden { get; set; }
        public bool OpenInNewTab { get; set; }
        public int SortOrder { get; set; } = 10;
        public string StartupCategory { get; set; } = "default";
        public List<string> AllowedCategories { get; set; } = new();
        public List<string> AllowedRoles { get; set; } = new();
        public List<MenuItem> Children { get; set; } = new();

        public MenuItem Clone()
        {
            return new MenuItem
            {
                Id = Id,
                ParentId = ParentId,
                MenuCode = MenuCode,
                DisplayName = DisplayName,
                IconKey = IconKey,
                UrlPath = UrlPath,
                IsHidden = IsHidden,
                OpenInNewTab = OpenInNewTab,
                SortOrder = SortOrder,
                StartupCategory = StartupCategory,
                AllowedCategories = AllowedCategories.ToList(),
                AllowedRoles = AllowedRoles.ToList(),
                Children = Children.Select(child => child.Clone()).ToList()
            };
        }
    }
}
