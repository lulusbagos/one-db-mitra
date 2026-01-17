using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using one_db_mitra.Data;

namespace one_db_mitra.Services.CompanyHierarchy
{
    public class CompanyHierarchyService
    {
        private readonly OneDbMitraContext _context;

        public CompanyHierarchyService(OneDbMitraContext context)
        {
            _context = context;
        }

        public async Task<string> BuildHierarchyPathAsync(int companyId, CancellationToken cancellationToken)
        {
            var info = await GetHierarchyInfoAsync(companyId, cancellationToken);
            if (info == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(info.Owner))
            {
                parts.Add(info.Owner);
            }
            if (info.LevelIndex >= 1 && !string.IsNullOrWhiteSpace(info.MainContractor))
            {
                parts.Add(info.MainContractor);
            }
            if (info.LevelIndex >= 2 && !string.IsNullOrWhiteSpace(info.SubContractor))
            {
                parts.Add(info.SubContractor);
            }
            if (info.LevelIndex >= 3 && !string.IsNullOrWhiteSpace(info.Vendor))
            {
                parts.Add(info.Vendor);
            }

            return string.Join(" - ", parts);
        }

        public async Task<HashSet<int>> GetDescendantCompanyIdsAsync(int companyId, CancellationToken cancellationToken)
        {
            var result = new HashSet<int>();
            if (companyId <= 0)
            {
                return result;
            }

            var rows = await _context.vw_hirarki_perusahaan_4level.AsNoTracking()
                .Where(r =>
                    r.id_owner == companyId
                    || r.id_main_contractor == companyId
                    || r.id_sub_contractor == companyId
                    || r.id_vendor == companyId)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                result.Add(companyId);
                return result;
            }

            foreach (var row in rows)
            {
                if (row.id_owner == companyId)
                {
                    AddIfValid(result, row.id_owner);
                    AddIfValid(result, row.id_main_contractor);
                    AddIfValid(result, row.id_sub_contractor);
                    AddIfValid(result, row.id_vendor);
                    continue;
                }

                if (row.id_main_contractor == companyId)
                {
                    AddIfValid(result, row.id_main_contractor);
                    AddIfValid(result, row.id_sub_contractor);
                    AddIfValid(result, row.id_vendor);
                    continue;
                }

                if (row.id_sub_contractor == companyId)
                {
                    AddIfValid(result, row.id_sub_contractor);
                    AddIfValid(result, row.id_vendor);
                    continue;
                }

                if (row.id_vendor == companyId)
                {
                    AddIfValid(result, row.id_vendor);
                }
            }

            if (result.Count == 0)
            {
                result.Add(companyId);
            }

            return result;
        }

        public async Task<Dictionary<int, HierarchyBadge>> BuildHierarchyLookupAsync(CancellationToken cancellationToken)
        {
            var rows = await _context.vw_hirarki_perusahaan_4level.AsNoTracking()
                .ToListAsync(cancellationToken);

            var lookup = new Dictionary<int, HierarchyBadge>();
            foreach (var row in rows)
            {
                AddBadge(lookup, row.id_owner, 0, row.owner, row.main_contractor, row.sub_contractor, row.vendor);
                AddBadge(lookup, row.id_main_contractor, 1, row.owner, row.main_contractor, row.sub_contractor, row.vendor);
                AddBadge(lookup, row.id_sub_contractor, 2, row.owner, row.main_contractor, row.sub_contractor, row.vendor);
                AddBadge(lookup, row.id_vendor, 3, row.owner, row.main_contractor, row.sub_contractor, row.vendor);
            }

            return lookup;
        }

        private async Task<HierarchyInfo?> GetHierarchyInfoAsync(int companyId, CancellationToken cancellationToken)
        {
            if (companyId <= 0)
            {
                return null;
            }

            var rows = await _context.vw_hirarki_perusahaan_4level.AsNoTracking()
                .Where(r =>
                    r.id_owner == companyId
                    || r.id_main_contractor == companyId
                    || r.id_sub_contractor == companyId
                    || r.id_vendor == companyId)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return null;
            }

            var match = rows.FirstOrDefault(r => r.id_vendor == companyId)
                ?? rows.FirstOrDefault(r => r.id_sub_contractor == companyId)
                ?? rows.FirstOrDefault(r => r.id_main_contractor == companyId)
                ?? rows.FirstOrDefault(r => r.id_owner == companyId);

            if (match == null)
            {
                return null;
            }

            var levelIndex = match.id_vendor == companyId
                ? 3
                : match.id_sub_contractor == companyId
                    ? 2
                    : match.id_main_contractor == companyId
                        ? 1
                        : 0;

            return new HierarchyInfo
            {
                LevelIndex = levelIndex,
                Owner = match.owner,
                MainContractor = match.main_contractor,
                SubContractor = match.sub_contractor,
                Vendor = match.vendor
            };
        }

        private static void AddIfValid(HashSet<int> set, int? value)
        {
            if (value.HasValue && value.Value > 0)
            {
                set.Add(value.Value);
            }
        }

        private static void AddBadge(Dictionary<int, HierarchyBadge> lookup, int? companyId, int levelIndex, string? owner, string? main, string? sub, string? vendor)
        {
            if (!companyId.HasValue || companyId.Value <= 0)
            {
                return;
            }

            lookup[companyId.Value] = new HierarchyBadge
            {
                LevelIndex = levelIndex,
                Owner = owner,
                MainContractor = main,
                SubContractor = sub,
                Vendor = vendor
            };
        }

        private class HierarchyInfo
        {
            public int LevelIndex { get; set; }
            public string? Owner { get; set; }
            public string? MainContractor { get; set; }
            public string? SubContractor { get; set; }
            public string? Vendor { get; set; }
        }

        public class HierarchyBadge
        {
            public int LevelIndex { get; set; }
            public string? Owner { get; set; }
            public string? MainContractor { get; set; }
            public string? SubContractor { get; set; }
            public string? Vendor { get; set; }
        }
    }
}
