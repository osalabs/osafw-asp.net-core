//load store.js first with const useFwStore

const mainApp = {
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
window.fwApp = app; //make app available for components below in html

const pinia = createPinia();
app.use(pinia);

// https://github.com/vueform/multiselect
//app.component('multiselect', VueformMultiselect);

//components - add load to vue_components

//mounted to #fw-app in /layout/vue/sys_footer
