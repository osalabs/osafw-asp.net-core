//load store.js first with const useMainStore

const mainApp = {
  store: useMainStore,
  // data() {
  //   return {
  //     counter: 0,
  //     layout: 1
  //   }
  // },
  computed:{
  },
  mounted() {
    //console.log('mainApp mounted');
    //TODO this.$store.commit('setBaseUrl', this.$el.parentElement.dataset.baseUrl );
    //TODO this.$store.commit('setRelatedId', this.$el.parentElement.dataset.relatedId );
    //TODO this.$store.dispatch('loadAll');
  },
  updated() {
    //console.log('mainApp updated');
  },
  methods:{
    reload(){
      window.location.reload();
    }
  }
};

const app = createApp(mainApp);
window.app = app;

const pinia = createPinia();
app.use(pinia);

// https://github.com/vueform/multiselect
//app.component('multiselect', VueformMultiselect);

//components - add load to vue_components

//mount in /layout/vue/sys_footer
