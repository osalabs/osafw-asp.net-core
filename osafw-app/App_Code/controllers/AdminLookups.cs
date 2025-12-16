// Lookups Manager Controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

namespace osafw;

public class AdminLookupsController : FwController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected FwControllers model = null!;

    public override void init(FW fw)
    {
        base.init(fw);

        base_url = "/Admin/Lookups";
        model = fw.model<FwControllers>();
    }

    public FwDict IndexAction()
    {
        FwDict ps = [];
        var rows = model.listGrouped(); //ordered by igroup (group name), iname, already filtered by access_level

        var cols = new FwList(); //will contain array of arrays with "list_groups" keys, which contains array of arrays with "list_rows" keys, which contains $row from $rows
        // one group must be in one column (no split groups between columns)
        // and we need to spread groups between 4 columns in a way so each column has relatively equal number of rows
        // so one column can have more than one group
        // each list_groups array should have "igroup" and "list_rows" keys
        var columns = 4;

        // 1) Group rows by igroup
        var grouped = new FwDict();
        foreach (FwDict row in rows)
        {
            var igroup = row["igroup"].toStr();
            if (!grouped.TryGetValue(igroup, out object? value))
            {
                value = new FwList();
                grouped[igroup] = value;
            }
            ((FwList)value!).Add(row);
        }

        // 2) Build an array of group-objects: [ 'igroup' => ..., 'list_rows' => [...] ]
        var allGroups = new FwList();
        foreach (var entry in grouped)
        {
            var gName = entry.Key;
            var gRows = (FwList)entry.Value!;
            allGroups.Add(new FwDict
            {
                ["igroup"] = gName,
                ["list_rows"] = gRows
            });
        }

        // Prepare empty columns
        for (int i = 0; i < columns; i++)
        {
            cols.Add(new FwDict
            {
                ["col_sm"] = (int)(12 / columns), // for Bootstrap's col-sm-x
                ["list_groups"] = new FwList()
            });
        }

        // Track how many rows are currently assigned to each column
        var colRowCounts = new int[columns];

        // 3) Distribute each group to the column with the smallest row count so far
        foreach (FwDict group in allGroups)
        {
            var gRows = (FwList)group["list_rows"]!;
            // Find the column with the smallest row count
            int targetColIndex = 0;
            for (int i = 1; i < columns; i++)
            {
                if (colRowCounts[i] < colRowCounts[targetColIndex])
                {
                    targetColIndex = i;
                }
            }
            // Assign the group to this column
            ((FwList)((FwDict)cols[targetColIndex]!)["list_groups"]!).Add(group);
            // Update the row count for this column
            colRowCounts[targetColIndex] += gRows.Count;
        }

        ps["list_cols"] = cols;
        return ps;
    }
}
