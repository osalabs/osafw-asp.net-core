//load store.js first with const useFwStore

const mainApp = {
  //data: () => ({
  //    counter: 0
  //}),
  computed:{
  },
  async mounted() {
    console.log('mainApp mounted');
    const fwStore = useFwStore();
    // assign all data from this.$el.parentElement.dataset to keys existing in fwStore
    fwStore.saveToStore(this.$el.parentElement.dataset);

    if (fwStore.current_screen){
      await fwStore.loadInitial();
      await fwStore.setCurrentScreen(fwStore.current_screen, fwStore.current_id);
    }else{
      fwStore.current_screen='list';
    }

    //handle back/forward browser nav
    window.addEventListener('popstate', (e) => {
      let state = window.history.state;
      if (state?.screen){
        fwStore.setCurrentScreen(state.screen, state.id);
      }
    })
  },
  updated() {
    //console.log('mainApp updated');
  },
  methods:{
    reload(){
      window.location.reload();
    },
  }
};

const app = createApp(mainApp);
window.fwApp = app; //make app available for components below in html

const pinia = createPinia();
app.use(pinia);

//components - add load to vue_components

//mounted to #fw-app in /layout/vue/sys_footer
