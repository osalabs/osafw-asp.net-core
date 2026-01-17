//load store.js first with const useFwStore

let mainApp = {
    //data: () => ({
    //    counter: 0
    //}),
    computed: {
    },
    setup() {
        const fwStore = useFwStore()
        return { fwStore } //everything returned here is reactive & template-visible
    },
    async mounted() {
        console.log('mainApp mounted');
        // assign all data from this.$el.parentElement.dataset to keys existing in fwStore
        this.fwStore.saveToStore(this.$el.parentElement.dataset);
        this.fwStore.initApi();

        await this.fwStore.loadInitial();
        if (this.fwStore.current_screen) {
            await this.fwStore.setCurrentScreen(this.fwStore.current_screen, this.fwStore.current_id);
        } else {
            this.fwStore.current_screen = 'list';
        }

        //handle back/forward browser nav
        window.addEventListener('popstate', (e) => {
            let state = window.history.state;
            if (state?.screen) {
                this.fwStore.setCurrentScreen(state.screen, state.id);
            }
        })

        this.fwStore.afterMounted();
    },
    updated() {
        //console.log('mainApp updated');
    },
    methods: {
        reload() {
            window.location.reload();
        },
    }
};

//merge in fwApp if defined
if (typeof fwApp !== 'undefined') {
    mainApp = AppUtils.deepMerge(mainApp, fwApp);
}

const app = createApp(mainApp);
app.use(createPinia());

window.fwApp = app; //make app available for components below in html

//components - add load to vue_components

//mounted to #fw-app in /layout/vue/sys_footer
