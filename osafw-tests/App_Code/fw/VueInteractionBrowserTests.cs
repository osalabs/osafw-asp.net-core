using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass]
public class VueInteractionBrowserTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));

    private static string Template(string name)
    {
        if (!Path.HasExtension(name)) name += ".html";
        return TemplatePath(Path.Combine("common/vue", name));
    }

    private static string TemplatePath(string relativePath)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "osafw-app/App_Data/template", relativePath));
        return Regex.Replace(content, @"<~/common/vue/([^>]+)>", match => Template(match.Groups[1].Value));
    }

    private static async Task<IPage> Page(IBrowser browser, string markup)
    {
        var assets = Environment.GetEnvironmentVariable("FW_BROWSER_ASSETS_ROOT") ?? Path.Combine(RepoRoot, "osafw-app/wwwroot/assets");
        if (!File.Exists(Path.Combine(assets, "lib/vue/vue.esm-browser.js"))) Assert.Inconclusive("Restore frontend libraries, or set FW_BROWSER_ASSETS_ROOT to a restored assets directory.");
        var context = await browser.NewContextAsync(new() { Offline = true });
        await context.RouteAsync("**/*", async route =>
        {
            var uri = new Uri(route.Request.Url);
            if (uri.Host != "vue-tests.invalid") { await route.AbortAsync(); return; }
            var relative = uri.AbsolutePath.TrimStart('/');
            if (relative == "") { await route.FulfillAsync(new() { ContentType = "text/html", Body = "<html><head></head><body></body></html>" }); return; }
            if (!relative.StartsWith("lib/") && relative != "js/apputils.js") { await route.AbortAsync(); return; }
            var file = Path.GetFullPath(Path.Combine(assets, relative));
            if (!file.StartsWith(Path.GetFullPath(assets) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { await route.AbortAsync(); return; }
            await route.FulfillAsync(new() { ContentType = "text/javascript", Body = await File.ReadAllTextAsync(file) });
        });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync("https://vue-tests.invalid/");
        var diagnostics = """
            <script>
            window.testErrors = [];
            window.addEventListener('error', event => testErrors.push(event.error?.message ?? event.message));
            window.addEventListener('unhandledrejection', event => testErrors.push(event.reason?.message ?? String(event.reason)));
            </script>
            """;
        var imports = """
            <script type="importmap">{"imports":{
                "vue":"/lib/vue/vue.esm-browser.js","pinia":"/lib/pinia/pinia.esm-browser.js",
                "Multiselect":"/lib/vueform-multiselect/dist/multiselect.js","vue-demi":"/lib/vue-demi/lib/index.mjs","@vue/devtools-api":"/lib/vue-devtools-api/dist/index.js",
                "@vue/devtools-shared":"/lib/vue-devtools-shared/dist/index.js","@vue/devtools-kit":"/lib/vue-devtools-kit/dist/index.js",
                "perfect-debounce":"/lib/perfect-debounce/dist/index.mjs","hookable":"/lib/hookable/dist/index.mjs","birpc":"/lib/birpc/dist/index.mjs"
            }}</script><script src="/js/apputils.js"></script>
            """;
        var setup = """
            <script type="module">
            import { createApp } from 'vue'; import { defineStore, createPinia } from 'pinia';
            const fwStoreState = {}, fwStoreGetters = {}, fwStoreActions = {};
            const mande = () => ({}); window.Toast = () => {};
            """ + Template("store.js") + """
            const pinia = createPinia(); window.testStore = useFwStore(pinia);
            window.testStore.handleError = () => {};
            window.testStore.api = { get: async () => ({}), post: async () => ({ id: 7 }), delete: async () => ({ id: 7 }) };
            window.fwApp = createApp({ template: '#test-root-template', setup: () => ({ fwStore: window.testStore }) }); fwApp.use(pinia);
            fwApp.config.globalProperties.AppUtils = AppUtils;
            fwApp.config.warnHandler = message => testErrors.push('Vue warning: ' + message);
            fwApp.config.errorHandler = error => testErrors.push('Vue error: ' + (error?.message ?? String(error)));
            fwApp.component('autocomplete', { props: ['modelValue'], emits: ['update:modelValue'], template: `<input :value="modelValue" @input="$emit('update:modelValue', $event.target.value)">` });
            for (const name of ['list-column-filter', 'list-cell-ro', 'list-cell-input', 'list-cell-date-combo', 'list-cell-select', 'list-cell-checkbox', 'form-control-help-block', 'att-select'])
                fwApp.component(name, { template: '<span></span>' });
            </script>
            """;
        var components = "";
        foreach (var name in new[] { "list-table-header.html", "list-row-btn.html", "list-table-row.html", "form-one-control.html", "form-one-group.html", "form-one-form-row.html", "form-one-row.html", "form-one-col.html", "form-one-fieldset.html", "form-one-def.html", "edit-form.html", "list-header.html" })
            components += Regex.Replace(Template(name), @"<~[^>]+>", "");
        components += Regex.Replace(TemplatePath("admin/demosvue/index/vue/subtable_demos_items.html"), @"<~[^>]+>", "");
        await page.SetContentAsync(diagnostics + imports + "<script type='text/x-template' id='test-root-template'>" + markup + "</script><div id='app'></div>" + setup + components + "<script type='module'>fwApp.mount('#app'); window.testReady=true;</script>");
        try { await page.WaitForFunctionAsync("() => window.testReady === true", options: new() { Timeout = 10000 }); }
        catch (TimeoutException) { Assert.Fail("Vue startup failed: " + string.Join("; ", errors)); }
        Assert.IsEmpty(errors, "Vue component scripts must load: " + string.Join("; ", errors));
        await AssertNoClientErrors(page);
        return page;
    }

    private static async Task AssertNoClientErrors(IPage page)
    {
        var errors = await page.EvaluateAsync<string[]>("() => window.testErrors");
        Assert.IsEmpty(errors, "Vue browser fixture reported client errors: " + string.Join("; ", errors));
    }

    private static async Task<IBrowser> Browser(IPlaywright playwright)
    {
        if (!File.Exists(playwright.Chromium.ExecutablePath)) Assert.Inconclusive("Install matching Chromium for Vue browser checks.");
        return await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chromium" });
    }

    [TestMethod, TestCategory("VueBrowser")]
    public async Task ColumnResizeSupportsKeyboardPointerBoundsAndCleanup()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await Browser(playwright);
        var page = await Page(browser, "<table><list-table-header v-if='fwStore.count !== -1'></list-table-header></table>");
        await page.EvaluateAsync("() => { testStore.list_headers=[{field_name:'title',field_name_visible:'Title'}]; testStore.count=1; window.widthSaves=[]; testStore.api.post=async (_, data) => { widthSaves.push(JSON.parse(data.widths)); return {}; }; }");
        var resize = page.GetByRole(AriaRole.Button, new() { Name = "Resize Title", Exact = true });
        await resize.PressAsync("End");
        await page.WaitForFunctionAsync("() => widthSaves.length === 1");
        Assert.AreEqual(800, await page.EvaluateAsync<int>("() => testStore.columnWidths().title"));
        await resize.PressAsync("Home");
        await page.WaitForFunctionAsync("() => widthSaves.length === 2");
        Assert.AreEqual(60, await page.EvaluateAsync<int>("() => testStore.columnWidths().title"));
        var box = (await resize.BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(box.X + 3, box.Y + 3);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X + 250, box.Y + 3);
        await page.Mouse.UpAsync();
        await page.WaitForFunctionAsync("() => widthSaves.length === 3");
        await resize.DblClickAsync();
        await page.WaitForFunctionAsync("() => widthSaves.length >= 4");
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => testStore.columnWidths().title >= 60 && testStore.columnWidths().title <= 800"));
        await page.Mouse.MoveAsync(box.X + 3, box.Y + 3);
        await page.Mouse.DownAsync();
        await page.EvaluateAsync("() => testStore.count = -1");
        var before = await page.EvaluateAsync<int>("() => widthSaves.length");
        await page.Mouse.UpAsync();
        Assert.AreEqual(before, await page.EvaluateAsync<int>("() => widthSaves.length"));
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => { testStore.list_user_view.widths = {unknown:99,title:900}; const widths=testStore.columnWidths(); return widths.title===800 && !(\"unknown\" in widths); }"));
        Assert.IsTrue(await page.EvaluateAsync<bool>("""
            () => {
                testStore.all_list_columns=Array.from({length:105},(_,index)=>({field_name:'field'+index}));
                testStore.list_headers=[testStore.all_list_columns[5]];
                testStore.list_user_view.widths=Object.fromEntries(testStore.all_list_columns.map((header,index)=>[header.field_name,index<5?0:100+index]));
                const widths=testStore.columnWidths();
                return Object.keys(widths).length===100 && !('field0' in widths) && widths.field104===204 && widths.field6===106;
            }
            """));
        Assert.IsTrue(await page.EvaluateAsync<bool>("""
            async () => {
                testStore.all_list_columns=[{field_name:'visible'},{field_name:'hidden'}];
                testStore.list_headers=[testStore.all_list_columns[0]];
                testStore.list_user_view.widths={visible:100,hidden:140}; testStore.is_list_edit=false;
                const oldCalls=[]; const newCalls=[]; let release;
                testStore.api={post:async (_,data)=>{oldCalls.push(data); await new Promise(resolve=>release=resolve); return {};}};
                const first=testStore.saveColumnWidth('visible',120);
                await new Promise(resolve=>setTimeout(resolve,0));
                testStore.api={post:async (_,data)=>{newCalls.push(data); return {};}}; testStore.is_list_edit=true;
                const second=testStore.saveColumnWidth('hidden',160); release(); await Promise.all([first,second]);
                return oldCalls.length===1 && oldCalls[0].is_list_edit===false && newCalls.length===1 && newCalls[0].is_list_edit===true
                    && JSON.parse(newCalls[0].widths).hidden===160;
            }
            """));
        Assert.IsTrue(await page.EvaluateAsync<bool>("""
            async () => {
                const previous={...testStore.list_user_view.widths};
                testStore.api={post:async()=>({success:false})};
                const saved=await testStore.saveColumnWidth('visible',300);
                return saved===false && JSON.stringify(testStore.list_user_view.widths)===JSON.stringify(previous);
            }
            """));
        await AssertNoClientErrors(page);
    }

    [TestMethod, TestCategory("VueBrowser")]
    public async Task ValidationRevealsTabAndFieldsetAndImmutableFieldsStayReadOnly()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await Browser(playwright);
        var page = await Page(browser, "<edit-form></edit-form>");
        await page.EvaluateAsync("""
            () => {
                testStore.current_screen='edit'; testStore.current_id=7;
                testStore.form_tabs=[{tab:'',label:'Main'},{tab:'details',label:'Details'}];
                testStore.showform_fields_tabs={'':[],details:[{type:'fieldset',label:'Values'},{field:'code',type:'input',label:'Code',immutable_on_edit:true},{field:'title',type:'input',label:'Title'},{type:'end_fieldset'}]};
                testStore.edit_data={id:7,i:{id:7,code:'Fixed',title:'Entered'},save_result:{error:{message:'Review'},validation_issues:[{severity:'error',field:'title',tab:'details',message:'Check title',value:'<img src=x onerror=alert(1)>'},{severity:'warning',field:'code',message:'Check code'}]}};
            }
            """);
        await page.GetByRole(AriaRole.Button, new() { Name = "Check title", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.activeElement?.closest('[data-fw-field]')?.dataset.fwField === 'title'");
        Assert.AreEqual(0, await page.Locator("[data-fw-field='code'] input").CountAsync());
        Assert.AreEqual(0, await page.Locator("img[src='x']").CountAsync());
        await page.GetByRole(AriaRole.Button, new() { Name = "Values", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Check title", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('.fw-fieldset.is-collapsed')");
        await page.EvaluateAsync("() => { testStore.current_id=0; testStore.edit_data.id=0; testStore.edit_data.i.id=0; testStore.form_tabs=[]; testStore.showform_fields=[{field:'code',type:'input',label:'Code',immutable_on_edit:true}]; }");
        await page.Locator("[data-fw-field='code'] input").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await AssertNoClientErrors(page);
    }

    [TestMethod, TestCategory("VueBrowser")]
    public async Task SaveFailuresWarningsDuplicateCallsPermissionsAndVisibilityKeepTheirContracts()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await Browser(playwright);
        var page = await Page(browser, "<list-header></list-header>");
        await page.EvaluateAsync("""
            () => {
                testStore.base_url='/Items'; testStore.current_screen='list'; testStore.is_list_edit_pane=true;
                testStore.edit_data={id:7,i:{id:7,title:'Draft'}};
                window.reloads=0; testStore.loadIndex=async () => reloads++;
                window.posts=0; testStore.api.post=async () => { posts++; return {error:{message:'No'},validation_issues:[{severity:'error',field:'title',message:'Invalid'}]}; };
            }
            """);
        await page.EvaluateAsync("async () => { await testStore.saveEditData(); }");
        Assert.AreEqual(0, await page.EvaluateAsync<int>("() => reloads"));
        await page.EvaluateAsync("async () => { testStore.api.post=async () => { posts++; await new Promise(resolve=>setTimeout(resolve,50)); return {id:7,validation_issues:[{severity:'warning',field:'title',message:'Review'}]}; }; await Promise.all([testStore.saveEditData(),testStore.saveEditData()]); }");
        Assert.AreEqual(2, await page.EvaluateAsync<int>("() => posts"));
        Assert.AreEqual(1, await page.EvaluateAsync<int>("() => reloads"));
        await page.EvaluateAsync("() => { testStore.capabilities={create:false,edit:false,delete:false}; }");
        Assert.AreEqual(0, await page.GetByRole(AriaRole.Button, new() { Name = "Add New", Exact = false }).CountAsync());
        await page.EvaluateAsync("async () => { await testStore.saveEditData(); }");
        Assert.AreEqual(2, await page.EvaluateAsync<int>("() => posts"));
        Assert.IsFalse(await page.EvaluateAsync<bool>("() => { testStore.list_rows=[{id:7},{id:8,_capabilities:{delete:false}}]; testStore.hchecked_rows={7:1,8:1}; return testStore.canDeleteSelection(); }"));
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => { testStore.capabilities.delete=true; testStore.hchecked_rows={7:1}; return testStore.canDeleteSelection(); }"));
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => { testStore.restoreFilterVisibility(); testStore.toggleFilterPanel(); testStore.restoreFilterVisibility(); return !testStore.is_filter_panel_open; }"));
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => { testStore.base_url='/Other'; testStore.restoreFilterVisibility(); return testStore.is_filter_panel_open; }"));
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => { Object.defineProperty(window,'localStorage',{get:()=>{throw new Error('blocked');}}); testStore.restoreFilterVisibility(); return testStore.is_filter_panel_open; }"));
        await AssertNoClientErrors(page);
    }

    [TestMethod, TestCategory("VueBrowser")]
    public async Task QuickEditSaveRefreshesListAndPreservesFocusedDraftContext()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await Browser(playwright);
        var page = await Page(browser, """
            <div class="list-edit-pane" style="height:60px;overflow:auto">
                <div style="height:100px"></div>
                <input v-if="fwStore.edit_data" id="quick-draft" v-model="fwStore.edit_data.i.title">
            </div>
            <div id="list-count">{{fwStore.count}}</div>
            <div id="first-row">{{fwStore.list_rows[0]?.title}}</div>
            """);
        await page.EvaluateAsync("""
            () => {
                testStore.current_screen='list'; testStore.current_id=7; testStore.is_list_edit_pane=true;
                testStore.quick_edit_keep_context=true; testStore.is_initial_load=false;
                testStore.edit_data={id:7,i:{id:7,title:'Draft changed'},subtables:{},save_result:{}};
                testStore.list_rows=[{id:7,title:'Before'}]; testStore.count=1;
                window.posts=0; window.gets=0; window.handled=[];
                testStore.handleError=(error, source) => handled.push(source);
                testStore.api.post=async () => { posts++; return {id:7}; };
                testStore.api.get=async () => { gets++; return {list_rows:[{id:7,title:'After'},{id:8,title:'Added'}],count:2}; };
            }
            """);
        var input = page.Locator("#quick-draft");
        await input.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await page.EvaluateAsync("""
            () => {
                const pane=document.querySelector('.list-edit-pane'); const input=document.querySelector('#quick-draft');
                input.focus(); input.setSelectionRange(2, 7); pane.scrollTop=75; window.expectedPaneScroll=pane.scrollTop;
            }
            """);
        await page.EvaluateAsync("async () => { await testStore.saveEditData(); }");
        Assert.AreEqual(1, await page.EvaluateAsync<int>("() => posts"));
        Assert.AreEqual(1, await page.EvaluateAsync<int>("() => gets"));
        Assert.AreEqual("2", await page.Locator("#list-count").TextContentAsync());
        Assert.AreEqual("After", await page.Locator("#first-row").TextContentAsync());
        Assert.AreEqual("Draft changed", await input.InputValueAsync());
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => document.activeElement?.id==='quick-draft' && document.activeElement.selectionStart===2 && document.activeElement.selectionEnd===7 && document.querySelector('.list-edit-pane').scrollTop===expectedPaneScroll"));
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => testStore.savedStatus === true"));

        await page.EvaluateAsync("""
            async () => {
                testStore.api.post=async () => { posts++; return {id:7}; };
                testStore.api.get=async () => { gets++; throw new Error('refresh unavailable'); };
                await testStore.saveEditData();
            }
            """);
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => testStore.savedStatus === true"));
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => testStore.formIssues().some(issue => issue.severity==='warning' && issue.message.includes('list could not refresh'))"));
        CollectionAssert.Contains(await page.EvaluateAsync<string[]>("() => handled"), "refreshQuickEditList");
        await AssertNoClientErrors(page);
    }

    [TestMethod, TestCategory("VueBrowser")]
    public async Task ChangedDraftDuringInflightSaveQueuesExactlyOneFollowup()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await Browser(playwright);
        var page = await Page(browser, "<input v-if='fwStore.edit_data' id='queued-draft' v-model='fwStore.edit_data.i.title'>");
        await page.EvaluateAsync("""
            () => {
                testStore.current_screen='edit'; testStore.current_id=7;
                testStore.edit_data={id:7,i:{id:7,title:'First draft'},subtables:{},save_result:{}};
                window.payloads=[]; window.releaseFirst=null;
                testStore.api.post=async (_, req) => {
                    payloads.push(JSON.parse(JSON.stringify(req)));
                    if (payloads.length===1) await new Promise(resolve => releaseFirst=resolve);
                    return {id:7};
                };
                window.firstSave=testStore.saveEditData();
            }
            """);
        await page.WaitForFunctionAsync("() => payloads.length===1 && testStore.is_saving_edit");
        await page.Locator("#queued-draft").FillAsync("Changed while saving");
        await page.EvaluateAsync("() => { testStore.saveEditData(); testStore.saveEditData(); releaseFirst(); }");
        await page.EvaluateAsync("async () => { await firstSave; }");
        Assert.AreEqual(2, await page.EvaluateAsync<int>("() => payloads.length"));
        Assert.AreEqual("First draft", await page.EvaluateAsync<string>("() => payloads[0].item.title"));
        Assert.AreEqual("Changed while saving", await page.EvaluateAsync<string>("() => payloads[1].item.title"));
        Assert.IsFalse(await page.EvaluateAsync<bool>("() => testStore.is_saving_edit || testStore.pending_edit_save"));
        await AssertNoClientErrors(page);
    }

    [TestMethod, TestCategory("VueBrowser")]
    public async Task FailedRowDeleteDoesNotReloadAndSuccessfulDeleteStillDoes()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await Browser(playwright);
        var page = await Page(browser, "<table><tbody><list-table-row v-if='fwStore.list_rows.length' :row='fwStore.list_rows[0]'></list-table-row></tbody></table>");
        await page.EvaluateAsync("""
            () => {
                testStore.list_rows=[{id:7,title:'Row'}]; testStore.list_headers=[];
                testStore.uioptions.list.table.isButtonsLeft=true;
                testStore.uioptions.list.table.rowButtons={view:false,edit:false,quickedit:false,delete:true,buttons:[]};
                window.deletes=0; window.reloads=0;
                testStore.loadIndexDebounced=async () => reloads++;
                testStore.api.delete=async () => { deletes++; return {success:false}; };
            }
            """);
        var delete = page.Locator(".list-row-controls a.text-danger");
        Assert.IsTrue((await delete.GetAttributeAsync("aria-label"))?.Contains("Delete", StringComparison.Ordinal) == true);
        await delete.DispatchEventAsync("click");
        await page.WaitForFunctionAsync("() => deletes===1");
        await page.WaitForTimeoutAsync(50);
        Assert.AreEqual(0, await page.EvaluateAsync<int>("() => reloads"));

        await page.EvaluateAsync("() => { testStore.api.delete=async () => { deletes++; return {id:7}; }; }");
        await delete.DispatchEventAsync("click");
        await page.WaitForFunctionAsync("() => reloads===1");
        Assert.AreEqual(2, await page.EvaluateAsync<int>("() => deletes"));
        await AssertNoClientErrors(page);
    }

    [TestMethod, TestCategory("VueBrowser")]
    public async Task DebouncedSaveDoesNotCrossFormsAndStillSavesTheActiveForm()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await Browser(playwright);
        var page = await Page(browser, "<div></div>");
        await page.EvaluateAsync("""
            () => {
                testStore.current_screen='edit'; testStore.current_id=7;
                testStore.edit_data={id:7,i:{id:7,title:'Old form'},subtables:{},save_result:{}};
                window.posts=[];
                testStore.api.post=async (_, req) => { posts.push(req.item.title); return {id:req.item.id}; };
                testStore.saveEditDataDebounced(30);
                testStore.current_id=8;
                testStore.edit_data={id:8,i:{id:8,title:'New form'},subtables:{},save_result:{}};
            }
            """);
        await page.WaitForTimeoutAsync(75);
        Assert.AreEqual(0, await page.EvaluateAsync<int>("() => posts.length"));

        await page.EvaluateAsync("() => testStore.saveEditDataDebounced(20)");
        await page.WaitForFunctionAsync("() => posts.length===1");
        CollectionAssert.AreEqual(new[] { "New form" }, await page.EvaluateAsync<string[]>("() => posts"));
        await AssertNoClientErrors(page);
    }

    [TestMethod, TestCategory("VueBrowser")]
    public async Task SubtableIssuesFocusTheMatchingRowAndImmutableExistingRowsOnly()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await Browser(playwright);
        var page = await Page(browser, """
            <form id="subtable-form" v-if="fwStore.edit_data">
                <button id="subtable-issue" type="button" @click="fwStore.focusFormIssue(fwStore.formIssues()[0], $event.currentTarget.closest('form'))">Review second notes</button>
                <subtable_demos_items
                    :def="{field:'lines',type:'subtable_edit',showform_fields:[{field:'demo_dicts_id',immutable_on_edit:true},{field:'iname',immutable_on_edit:true},{field:'idesc',immutable_on_edit:true},{field:'is_checkbox',immutable_on_edit:true}]}"
                    :lookups="fwStore.lookups" :form="fwStore.edit_data"></subtable_demos_items>
            </form>
            """);
        await page.EvaluateAsync("""
            () => {
                testStore.current_screen='edit'; testStore.current_id=7;
                testStore.lookups={DemoDicts:[{id:1,iname:'One'},{id:2,iname:'Two'}]};
                testStore.edit_data={id:7,i:{id:7},subtables:{lines:[
                    {id:11,demo_dicts_id:1,iname:'First',idesc:'First notes',is_checkbox:1},
                    {id:22,demo_dicts_id:2,iname:'Second',idesc:'Second notes',is_checkbox:0}
                ]},save_result:{validation_issues:[{severity:'error',field:'item-lines#22[idesc]',row_id:'22',message:'Review second notes'}]}};
            }
            """);
        var rows = page.Locator("#subtable-form tbody tr");
        await rows.Nth(1).WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await page.Locator("#subtable-issue").ClickAsync();
        await page.WaitForFunctionAsync("() => document.activeElement?.closest('[data-fw-row]')?.dataset.fwRow==='22'");
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => document.activeElement?.tagName==='TEXTAREA' && document.activeElement.closest('[data-fw-field]')?.dataset.fwField==='item-lines#22[idesc]'"));

        var existing = rows.Nth(0);
        Assert.IsTrue(await existing.Locator("select").IsDisabledAsync());
        Assert.IsTrue(await existing.Locator("textarea").IsEditableAsync() == false);
        Assert.IsTrue(await existing.Locator("input[type=checkbox]").IsDisabledAsync());
        Assert.AreEqual(0, await existing.Locator("input:not([type=checkbox])").CountAsync());

        await page.GetByRole(AriaRole.Button, new() { Name = "Add More", Exact = true }).ClickAsync();
        var added = rows.Nth(2);
        await added.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Assert.IsFalse(await added.Locator("select").IsDisabledAsync());
        Assert.IsTrue(await added.Locator("textarea").IsEditableAsync());
        Assert.IsFalse(await added.Locator("input[type=checkbox]").IsDisabledAsync());
        Assert.AreEqual(1, await added.Locator("input:not([type=checkbox])").CountAsync());
        Assert.IsTrue(await page.EvaluateAsync<bool>("() => document.activeElement?.closest('tr') === document.querySelector('#subtable-form tbody tr:last-child')"));
        await AssertNoClientErrors(page);
    }
}
