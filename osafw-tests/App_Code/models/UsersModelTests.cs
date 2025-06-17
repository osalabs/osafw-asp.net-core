using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using System.Collections;
using System.Collections.Generic;

namespace osafw.Tests;

public class InMemoryUsersModel : osafw.Users
{
    private readonly Dictionary<int, Hashtable> store = new();
    private int lastId = 0;

    public override int add(Hashtable item)
    {
        lastId++;
        item = (Hashtable)item.Clone();
        item["id"] = lastId;
        if (item.ContainsKey("pwd"))
            item["pwd"] = this.hashPwd((string)item["pwd"]);
        store[lastId] = item;
        return lastId;
    }

    public override bool update(int id, Hashtable item)
    {
        if (!store.ContainsKey(id)) return false;
        foreach (DictionaryEntry de in item)
        {
            if (de.Key.Equals("pwd"))
                store[id][de.Key] = this.hashPwd((string)de.Value);
            else
                store[id][de.Key] = de.Value;
        }
        return true;
    }

    public override osafw.DBRow one(int id)
    {
        return store.ContainsKey(id) ? new osafw.DBRow(store[id]) : new osafw.DBRow();
    }

    public override void delete(int id, bool is_perm = false)
    {
        store.Remove(id);
    }

    public override osafw.DBList list(IList statuses = null)
    {
        osafw.DBList result = new();
        foreach (var ht in store.Values)
            result.Add(new osafw.DBRow(ht));
        return result;
    }
}

[TestClass]
public class UsersModelTests
{
    private InMemoryUsersModel model = null!;

    [TestInitialize]
    public void Setup()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var fw = osafw.FW.initOffline(config);
        model = new InMemoryUsersModel();
        model.init(fw);
    }

    [TestMethod]
    public void AddUpdateDeleteUser()
    {
        Hashtable user = osafw.DB.h("fname", "John", "lname", "Doe", "email", "john@example.com", "pwd", "secret");
        int id = model.add(user);
        Assert.IsTrue(id > 0);
        var added = model.one(id);
        Assert.AreEqual("John", added["fname"]);

        model.update(id, osafw.DB.h("lname", "Smith"));
        var updated = model.one(id);
        Assert.AreEqual("Smith", updated["lname"]);

        model.delete(id, true);
        var deleted = model.one(id);
        Assert.AreEqual(0, deleted.Count);
    }
}
