<script type="module">
import { createApp } from 'vue';
import { VueDraggableNext } from 'vue-draggable-next';

const el = document.getElementById('fw-app');
const base_url = el.dataset.base_url;
const controller = el.dataset.controller;
const xss = el.dataset.xss;
let config = {};
try { config = JSON.parse(el.dataset.config || '{}'); } catch(e) { config = {}; }

const TYPES_SHOW = [
  'row','col','col_end','row_end','header','plaintext','plaintext_link','plaintext_autocomplete','plaintext_yesno','plaintext_currency','markdown','noescape','float','checkbox','date','date_long','multi','multi_prio','att','att_links','att_files','subtable','added','updated'
];
const TYPES_SHOWFORM = [
  'row','col','col_end','row_end','header','group_id','group_id_addnew','select','input','textarea','email','number','password','currency','autocomplete','multicb','multicb_prio','radio','yesno','cb','date_popup','date_combo','datetime_popup','time','att_edit','att_links_edit','att_files_edit','subtable_edit'
];

createApp({
  data() {
    return { controller, config };
  },
  components: { draggable: VueDraggableNext },
  methods: {
    addField(list){
      if(!this.config[list]) this.$set(this.config, list, []);
      this.config[list].push({ field:'', label:'', type:'' });
    },
    removeField(list, idx){
      this.config[list].splice(idx,1);
    },
    async save(){
      const res = await fetch(base_url+'/(ConfigEditorSave)', {
        method:'POST',
        headers:{'Content-Type':'application/json'},
        body: JSON.stringify({ controller:this.controller, config:this.config, XSS:xss })
      });
      const data = await res.json();
      if(data.success) Toast('Config saved', { theme:'text-bg-success' });
      else Toast('Error saving', { theme:'text-bg-danger' });
    }
  },
  template: `
<div v-if="controller">
  <div class="row">
    <div class="col-md-6">
      <h5>show_fields</h5>
      <draggable v-model="config.show_fields" class="list-group">
        <div class="list-group-item" v-for="(f,idx) in config.show_fields" :key="idx">
          <div class="row g-1 align-items-center">
            <div class="col"><input v-model="f.field" placeholder="field" class="form-control form-control-sm"></div>
            <div class="col"><input v-model="f.label" placeholder="label" class="form-control form-control-sm"></div>
            <div class="col">
              <select v-model="f.type" class="form-select form-select-sm">
                <option v-for="t in TYPES_SHOW" :value="t">{{t}}</option>
              </select>
            </div>
            <div class="col-auto"><button type="button" class="btn btn-sm btn-outline-danger" @click="removeField('show_fields',idx)">&times;</button></div>
          </div>
        </div>
      </draggable>
      <button class="btn btn-sm btn-primary mt-2" @click="addField('show_fields')">Add Field</button>
    </div>
    <div class="col-md-6">
      <h5>showform_fields</h5>
      <draggable v-model="config.showform_fields" class="list-group">
        <div class="list-group-item" v-for="(f,idx) in config.showform_fields" :key="idx">
          <div class="row g-1 align-items-center">
            <div class="col"><input v-model="f.field" placeholder="field" class="form-control form-control-sm"></div>
            <div class="col"><input v-model="f.label" placeholder="label" class="form-control form-control-sm"></div>
            <div class="col">
              <select v-model="f.type" class="form-select form-select-sm">
                <option v-for="t in TYPES_SHOWFORM" :value="t">{{t}}</option>
              </select>
            </div>
            <div class="col-auto"><button type="button" class="btn btn-sm btn-outline-danger" @click="removeField('showform_fields',idx)">&times;</button></div>
          </div>
        </div>
      </draggable>
      <button class="btn btn-sm btn-primary mt-2" @click="addField('showform_fields')">Add Field</button>
    </div>
  </div>
  <div class="mt-3">
    <button class="btn btn-success" @click="save">Save Config</button>
  </div>
</div>
<div v-else>Select controller to edit.</div>
`
}).mount('#fw-app');
</script>
