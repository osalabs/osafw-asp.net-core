/*
  misc client utils for the osafw framework
  www.osalabs.com/osafw
  (c) 2009-2018 Oleg Savchuk www.osalabs.com
*/

window.fw={
  HTML_LOADING : '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Loading...',
  ICON_INFO: '<svg width="1em" height="1em" viewBox="0 0 16 16" class="bi bi-info-circle-fill" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M8 16A8 8 0 1 0 8 0a8 8 0 0 0 0 16zm.93-9.412l-2.29.287-.082.38.45.083c.294.07.352.176.288.469l-.738 3.468c-.194.897.105 1.319.808 1.319.545 0 1.178-.252 1.465-.598l.088-.416c-.2.176-.492.246-.686.246-.275 0-.375-.193-.304-.533L8.93 6.588zM8 5.5a1 1 0 1 0 0-2 1 1 0 0 0 0 2z"/></svg>',
  ICON_QUEST: '<svg width="1em" height="1em" viewBox="0 0 16 16" class="bi bi-question-circle-fill" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0zM5.496 6.033a.237.237 0 0 1-.24-.247C5.35 4.091 6.737 3.5 8.005 3.5c1.396 0 2.672.73 2.672 2.24 0 1.08-.635 1.594-1.244 2.057-.737.559-1.01.768-1.01 1.486v.105a.25.25 0 0 1-.25.25h-.81a.25.25 0 0 1-.25-.246l-.004-.217c-.038-.927.495-1.498 1.168-1.987.59-.444.965-.736.965-1.371 0-.825-.628-1.168-1.314-1.168-.803 0-1.253.478-1.342 1.134-.018.137-.128.25-.266.25h-.825zm2.325 6.443c-.584 0-1.009-.394-1.009-.927 0-.552.425-.94 1.01-.94.609 0 1.028.388 1.028.94 0 .533-.42.927-1.029.927z"/></svg>',
  ICON_SORT_ASC: '<svg width="1em" height="1em" viewBox="0 0 16 16" class="bi bi-arrow-down" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M8 1a.5.5 0 0 1 .5.5v11.793l3.146-3.147a.5.5 0 0 1 .708.708l-4 4a.5.5 0 0 1-.708 0l-4-4a.5.5 0 0 1 .708-.708L7.5 13.293V1.5A.5.5 0 0 1 8 1z"/></svg>',
  ICON_SORT_DESC: '<svg width="1em" height="1em" viewBox="0 0 16 16" class="bi bi-arrow-up" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M8 15a.5.5 0 0 0 .5-.5V2.707l3.146 3.147a.5.5 0 0 0 .708-.708l-4-4a.5.5 0 0 0-.708 0l-4 4a.5.5 0 1 0 .708.708L7.5 2.707V14.5a.5.5 0 0 0 .5.5z"/></svg>',
  MODAL_ALERT: '<div class="modal fade" tabindex="-1" role="dialog" id="fw-modal-alert"><div class="modal-dialog modal-sm" role="document"><div class="modal-content"><div class="modal-header"><h5 class="modal-title"></h5><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div><div class="modal-body"><p></p></div><div class="modal-footer"><button type="button" class="btn btn-primary btn-block" data-bs-dismiss="modal">OK</button></div></div></div></div>',
  MODAL_CONFIRM: '<div class="modal fade" tabindex="-1" role="dialog" id="fw-modal-confirm"><div class="modal-dialog modal-sm" role="document"><div class="modal-content"><div class="modal-header"><h5 class="modal-title"></h5><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div><div class="modal-body"><p></p></div><div class="modal-footer"><button type="button" class="btn btn-primary" data-bs-dismiss="modal">OK</button><button type="button" class="btn btn-default" data-bs-dismiss="modal">Cancel</button></div></div></div></div>',

  ok: function (str, options){
    options = $.extend({}, {theme: 'hint_green'}, options);
    // if (typeof(options)=='undefined') options={theme: 'hint_green'};
    $.jGrowl(str, options);
  },

  error: function (str, options){
    options = $.extend({}, {theme: 'hint_error'}, options);
    // if (typeof(options)=='undefined') options={theme: 'hint_error'};
    $.jGrowl(str, options);
  },

  // usage: fw.alert('Process completed','Worker');
  alert: function (content, title){
    if (!title) title=fw.ICON_INFO+' Alert';
    var $modal=$('#fw-modal-alert');
    if (!$modal.length){//add template to document
      $(document.body).append(fw.MODAL_ALERT);
      $modal=$('#fw-modal-alert');
    }
    $modal.modal('show').find('.modal-title').html(title).end().find('.modal-body p').html(content);
    $modal.off('shown.bs.modal').on('shown.bs.modal', function (e) {
      $modal.find('.btn-primary').focus();
    });
  },

  /*
  fw.confirm('Are you sure?', 'optional title', function(){
    //proceed OK answer
  });
  */
  confirm: function (content, title_or_cb, callback){
    if ($.isFunction(title_or_cb)){
      title='';
      callback=title_or_cb;
    }else{
      title=title_or_cb;
    }
    if (!title) title=fw.ICON_QUEST+' Confirm';
    var $modal=$('#fw-modal-confirm');
    if (!$modal.length){//add template to document
      $(document.body).append(fw.MODAL_CONFIRM);
      $modal=$('#fw-modal-confirm');
    }
    $modal.modal('show').find('.modal-title').html(title).end().find('.modal-body p').html(content);
    $modal.off('shown.bs.modal').on('shown.bs.modal', function (e) {
      $modal.find('.btn-primary').focus();
    });
    $modal.find('.btn-primary').off('click').one('click', function (e) {
      callback();
    });
  },

  //toggle on element between mutliple classes in order
  toggleClasses($el, arr_classes){
    let cur_index = -1;

    for (let i = 0; i < arr_classes.length; i++) {
        if ($el.hasClass(arr_classes[i])) {
            cur_index = i;
            break;
        }
    }

    // Remove the current class
    if (cur_index !== -1) {
        $el.removeClass(arr_classes[cur_index]);
    }

    // Add the next class in the array or go back to the start if we're at the end
    let nextClassIndex = (cur_index + 1) % arr_classes.length;
    $el.addClass(arr_classes[nextClassIndex]);
  },

  //debounce helper
  debounce(func, wait_msecs) {
    let timeout;

    return function executedFunction(...args) {
      const later = () => {
          clearTimeout(timeout);
          func(...args);
      };

      clearTimeout(timeout);
      timeout = setTimeout(later, wait_msecs);
    };
  },

  //called on document ready
  setup_handlers: function (){
    //list screen init
    fw.make_table_list(".list");

    //list screen init
    var $ffilter = $('form[data-list-filter]:first');

    //advanced search filter
    var on_toggle_search = function (e) {
      var $fis = $ffilter.find('input[name="f[is_search]"]');
      var $el = $('table.list .search');
      if ($el.is(':visible')){
          $el.hide();
          $fis.val('');
      } else {
          $el.show();
          $fis.val('1');
          //show search tooltip
          $.jGrowl("WORD to search for contains word<br>"+
            "!WORD to search for NOT contains word<br>"+
            "=WORD to search for equals word<br>"+
            "!=WORD to search for NOT equals word<br>"+
            "&lt;=N, &lt;N, &gt;=N, &gt;N - compare numbers",
            {header: 'Search hints', theme: 'hint_info', sticky: true});
      }
    };
    $(document).on('click', '.on-toggle-search', on_toggle_search);
    //open search if there is something
    var is_search = $('table.list .search input').filter(function () {
      return this.value.length > 0;
    }).length>0;
    if (is_search){
      on_toggle_search();
    }

    //list table density switch
    var on_toggle_density = function (e) {
      const $this=$(this);
      const $tbl = $this.closest('table.list');
      const $wrapper = $tbl.closest('.table-list-wrapper');
      const classes = ['table-sm', 'table-dense', 'table-normal'];

      fw.toggleClasses($tbl, classes);
      if ($tbl.is('.table-dense')){
        $wrapper.addClass('table-dense');
      }else{
        $wrapper.removeClass('table-dense');
      }
      let density_class = classes.find(cls => $tbl.hasClass(cls)) || '';

      //ajax post to save user preference to current url/(SaveUserViews) or custom url
      const url = $this.data('url') || (window.location.pathname.replace(/\/$/, "") + "/(SaveUserViews)");
      $.ajax({
          url: url,
          type: 'POST',
          dataType: 'json',
          data: {
              density: density_class,
              XSS: $this.closest('form').find("input[name=XSS]").val()
          },
          success: function (data) {
            //console.log(data);
          },
          error: function (e) {
            console.error("An error occurred while saving user preferences:", e.statusText);
          }
      });
    };
    $(document).on('click', '.on-toggle-density', on_toggle_density);

    $('table.list').on('keypress','.search :input', function(e) {
      if (e.which == 13) {// on Enter press
          e.preventDefault();
          //on explicit search - could reset pagenum to 0
          //$ffilter.find('input[name="f[pagenum]"]').val(0);
          $ffilter.trigger('submit');
          return false;
      }
    });

    //on filter form submit - add advanced search fields into form
    $ffilter.on('submit', function (e) {
        var $f = $ffilter;
        var $fis = $f.find('input[name="f[is_search]"]');
        if ($fis.val()=='1'){
            //if search ON - add search fields to the form
            $f.find('.osafw-list-search').remove();
            var html=[];
            $('table.list:first .search :input').each(function (i, el) {
              if (el.value>''){
                html.push('<input class="osafw-list-search" type="hidden" name="'+el.name.replace(/"/g,'&quot;')+'" value="'+el.value.replace(/"/g,'&quot;')+'">');
              }
            });
            $f.append(html.join(''));
        }
    });

    //autosubmit filter on change filter fields
    $(document).on('change', 'form[data-list-filter][data-autosubmit] [name^="f["]:input:visible:not([data-nosubmit])', function(){
        this.form.submit();
    });

    //pager click via form filter submit so all filters applied
    $(document).on('click', '.pagination .page-link[data-pagenum]', function (e){
      var $this = $(this);
      var pagenum = $this.data('pagenum');
      var $f = $this.data('filter') ? $($this.data('filter')) : $('form[data-list-filter]:first');
      if ($f){
        e.preventDefault();
        $('<input type="hidden" name="f[pagenum]">').val(pagenum).appendTo($f);
        $f.submit();
      }
    });

    //pagesize change
    $(document).on('change', '.on-pagesize-change', function (e){
      e.preventDefault();
      var $this = $(this);
      var $f = $this.data('filter') ? $($this.data('filter')) : $('form[data-list-filter]:first');
      if ($f){
        $f.find('input[name="f[pagesize]"]').val($this.val());
        $f.submit();
      }
    });

    //list check all/none handler
    $(document).on('click', '.on-list-chkall', function (e){
      var $cbs = $(".multicb", this.form).prop('checked', this.checked);
      if (this.checked){
        $cbs.closest("tr").addClass("selected");
      }else{
        $cbs.closest("tr").removeClass("selected");
      }
    });

    //make list multi buttons floating if at least one row checked
    $(document).on('click', '.on-list-chkall, .multicb', function (e) {
      e.stopPropagation();//prevent tr click handler
      var $this = $(this);
      var $bm = $('#list-btn-multi');
      var len = $('.multicb:checked').length;
      if (len>0){
        //float
        $bm.addClass('position-sticky');
        $bm.find('.rows-num').text(len);
      }else{
        //de-float
        $bm.removeClass('position-sticky');
        $bm.find('.rows-num').text('');
      }
      if ($this.is(".multicb")){
        if (this.checked){
          $this.closest("tr").addClass("selected");
        }else{
          $this.closest("tr").removeClass("selected");
        }
      }
    });

    //click on row - select/unselect row
    $(document).on('click', 'table.list > tbody > tr', function (e) {
      var $this = $(this);
      var tag_name = e.target.tagName.toLowerCase();
      if (tag_name === 'a'||tag_name === 'button'){
        return; // do not process if link/button clicked
      }
      $this.find('.multicb:first').click();
    });

    $(document).on('click', '.on-delete-list-row', function (e){
      e.preventDefault();
      fw.delete_btn(this);
    });

    $(document).on('click', '.on-delete-multi', function (e){
      var el=this;
      if (!el._is_confirmed){
        e.preventDefault();
        if (!$('.multicb:checked').length) return;//exit if no rows selected

        fw.confirm('Are you sure to delete multiple selected records?', function(){
          el._is_confirmed=true;
          $(el).click();//trigger again after confirmed
        });
      }
    });


    //form screen init
    fw.setup_cancel_form_handlers();
    fw.setup_autosave_form_handlers();
    fw.process_form_errors();
    fw.setup_file_drop_area();

    $(document).on('change', '.on-refresh', function (e) {
      var $f = $(this).closest('form');
      $f.find('input[name=refresh]').val(1);
      $f.submit();
    });

    $(document).on('keyup', '.on-multi-search', function (e) {
      var $this = $(this);
      var s = $this.val().replace(/"/g, '').toUpperCase();
      var $div = $this.closest('.field-multi-value');
      var $cb = $div.find('[data-s]');
      if (s>''){
          $cb.hide();
          $cb.filter('[data-s*="'+s+'"]').show();
      }else{
          $cb.show();
      }
    });

    //on click - confirm, then submit via POST
    //ex: <button type="button" class="btn btn-default on-fw-submit" data-url="SUBMIT_URL?XSS=<~SESSION[XSS]>" data-title="CONFIRMATION TITLE"></button>
    $(document).on('click', '.on-fw-submit', function (e) {
        e.preventDefault();
        var $this=$(this);
        var url = $this.data('url');
        var title = $this.data('title');
        if (!title) title='Are you sure?';
        fw.confirm(title, function (e) {
            $('#FTmpSubmit').remove();
            $(document.body).append('<form id="FTmpSubmit" method="POST" action="'+url+'"></form>');
            $('#FTmpSubmit').submit();
        });
    });


  },

  //for all forms with data-check-changes on a page - setup changes tracker, call in $(document).ready()
  // <form data-check-changes>
  setup_cancel_form_handlers: function() {
    //on submit buttons handler
    // <button type="button" data-target="#form" class="on-submit" [data-delay="300"] [data-refresh] [name="route_return" value="New"]>Submit</button>
    $(document).on('click', '.on-submit', function (e) {
      e.preventDefault();
      var $this=$(this);
      var target = $this.data('target');
      var $form = (target) ? $(target) : $(this.form);

      //if has data-refresh - set refresh
      if ($this.data().hasOwnProperty('refresh')){
        $form.find('input[name=refresh]').val(1);
      }

      //if button has a name - add it as parameter to submit form
      var bname = $this.attr('name');
      if (bname>''){
        var bvalue = $this.attr('value');
        var $input = $form.find('input[name="' + bname + '"]');
        if (!$input.length) {
          $input = $('<input type="hidden" name="' + bname + '">').appendTo($form);
        }
        $input.val(bvalue);
      }

      //if button has data-delay - submit with delay (in milliseconds)
      var delay = $this.data('delay');
      if (delay) {
         setTimeout(function () {
             $form.submit();
         }, delay);
        }
      else {
        $form.submit();
      }
    });

    //on cancel buttons handler
    // <a href="url" class="on-cancel">Cancel</a>
    // // <button type="button" data-href="url" class="on-cancel">Cancel</button>
    $(document).on('click', '.on-cancel', function (e) {
      e.preventDefault();
      var $this=$(this);
      var target = $this.data('target');
      var $form = (target) ? $(target) : $(this.form);
      var url = $this.prop('href');
      if (!url) url = $this.data('href');
      fw.cancel_form($form, url);
    });

    var $forms=$('form[data-check-changes]');
    if (!$forms.length) return; //skip if no forms found

    $(document.body).on('change', 'form[data-check-changes]', function(){
      $(this).data('is-changed', true);
    });
    $(document.body).on('submit', 'form[data-check-changes]', function(){
      //on form submit - disable check for
      $(this).data('is-changed-submit', true);
    });
    $(window).on('beforeunload', function (e) {
      var is_changed = false;
      $forms.each(function(index, el) {
          var $form = $(el);
          if ( $form.data('is-changed')===true ){
              if ( $form.data('is-changed-submit')===true ) {
                  //it's a form submit - no need to check this form changes. Only if changes occurs on other forms, we'll ask
              }else{
                  is_changed=true;
                  return false;
              }
          }
      });
      if (is_changed){
          return 'There are unsaved changes.\nDo you really want to Cancel editing the Form?';
      }
    });
  },

  //check if form changed
  // if yes - confirm before going to other url
  // if no - go to url
  cancel_form: function(f, url){
    var $f=$(f);

    if ($f.data('is-ajaxsubmit')===true){
      //if we are in the middle of autosave - wait a bit
      setTimeout(function(){
        fw.cancel_form(f, url);
      },500);
      return false;
    }

    if ( $f.data('is-changed')===true ){
      fw.confirm('<strong>There are unsaved changes.</strong><br>Do you really want to Cancel editing the Form?', function (){
        $f.data('is-changed', false);//force false so beforeunload will pass
        window.location=url;
      });
    }else{
      window.location=url;
    }
    return false;
  },

  //tries to auto save form via ajax on changes
  // '.form-saved-status' element updated with save status
  // <form data-autosave>
  setup_autosave_form_handlers: function () {
    var $asforms=$('form[data-autosave]');
    if (!$asforms.length) return; //skip if no autosave forms found

    //prevent submit if form is in ajax save
    $asforms.on('submit', function (e) {
      var $f = $(this);
      if ($f.data('is-ajaxsubmit')===true){
          //console.log('FORM SUBMIT CANCEL due to autosave in progress');
          e.preventDefault();
          return false;
      }
      $f.data('is-submitting',true); //tell autosave not to trigger
    });

    //when some input into the form happens - trigger autosave in 30s
    var to_autosave;
    $(document.body).on('input', 'form[data-autosave]', function(e){
      var $control = $(e.target);
      if ($control.is('[data-noautosave]')) {
        e.preventDefault();
        return;
      }

      var $f = $(this);
      //console.log('on form input', $f, e);
      set_saved_status($f, true);

      //refresh timeout
      clearTimeout(to_autosave);
      to_autosave = setTimeout(function(){
        //console.log('triggering autosave after 30s idle');
        trigger_autosave_if_changed($f);
      }, 30000);
    });

    //when change or blur happens - trigger autosave now(debounced)
    $(document.body).on('change', 'form[data-autosave]', function(e){
      if ($(e.target).is('[data-noautosave]')) {
        e.preventDefault();
        return;
      }
      var $f = $(this);
      //console.log('on form change', $f, e);
      $f.trigger('autosave');
    });
    // "*:not(.bs-searchbox)" - exclude search input in the bs selectpicker container
    $(document.body).on('blur', 'form[data-autosave] *:not(.bs-searchbox) > :input:not(button,[data-noautosave])', function(e){
      var $f = $(this.form);
      //console.log('on form input blur', $f);
      trigger_autosave_if_changed($f);
    });

    $(document.body).on('autosave', 'form[data-autosave]', function(e){
      //debounced autosave
      var $f = $(this);
      clearTimeout($f[0]._to_autosave);
      $f[0]._to_autosave = setTimeout(function(){
        //console.log('triggering autosave after 50ms');
        form_autosave($f);
      }, 500);
    });

    function form_reset_state($f) {
      set_progress($f, false);
      $f.data('is-ajaxsubmit', false);
    }

    function form_handle_errors($f, data, hint_options){
      if (data.ERR) {
        //auto-save error - highlight errors
        fw.process_form_errors($f, data.ERR);
      }
      fw.error(data.err_msg || 'Auto-save error. Server error occurred. Try again later.', hint_options);
    }

    function form_autosave($f) {
      if ($f.data('is-submitting')===true || $f.data('is-ajaxsubmit')===true){
          //console.log('on autosave - ALREADY SUBMITTING');
          //if form already submitting by user intput - schedule autosave again later
          $f.trigger('autosave');
          return false;
      }
      //console.log('on autosave', $f);
      $f.data('is-ajaxsubmit',true);
      var hint_options={};
      if ($f.data('autosave-sticky')){
        hint_options={sticky: true};
      }

      //console.log('before ajaxSubmit', $f);
      set_progress($f, true);
      $f.ajaxSubmit({
          dataType: 'json',
          success: function (data) {
              form_reset_state($f);
              //console.log('ajaxSubmit success', data);
              $('#fw-form-msg').hide();
              fw.clean_form_errors($f);
              if (data.success){
                  set_saved_status($f, false);
                  if (data.is_new && data.location) {
                      window.location = data.location; //reload screen for new items
                  }
              }else{
                  form_handle_errors($f, data, hint_options);
              }
              if (data.msg) fw.ok(data.msg, hint_options);
              $f.trigger('autosave-success',[data]);
          },
          error: function (e) {
              form_reset_state($f);
              // console.log('ajaxSubmit error', e);
              let data = e.responseJSON??{};
              form_handle_errors($f, data, hint_options);
              $f.trigger('autosave-error',[e]);
          }
      });
    }

    function trigger_autosave_if_changed($f){
      if ($f.data('is-changed')===true){
          //form changed, need autosave
          $f.trigger('autosave');
      }
    }

    function set_saved_status($f, is_changed){
      $f=$($f);
      if ($f.data('is-changed')===is_changed){
        return;//no changes
      }

      var $html = $('<span>').append($f[0]._saved_status);
      var spinner = $('<span>').append($html.find('.spinner-border').clone()).html();
      var cls='', txt='';
      if (is_changed==true){ //not saved
          $f.data('is-changed', true);
          cls='bg-danger';
          txt='not saved';
      }else if (is_changed==false){ //saved
          $f.data('is-changed', false);
          cls='bg-success';
          txt='saved';
      }
      var html=spinner+'<span class="badge '+cls+'">'+txt+'</span>';
      $f[0]._saved_status=html;
      $f.find('.form-saved-status').html(html);
      $('.form-saved-status-global').html(html);
    }

    function set_progress($f, is_working){
      $f=$($f);
      var $html = $('<span>').append($f[0]._saved_status);
      var is_spinner = $html.find('.spinner-border').length>0;

      if (is_working){
        if (!is_spinner) $html.prepend('<span class="spinner-border spinner-border-sm mr-2" role="status" aria-hidden="true"></span>');
      }else{
        if (is_spinner) $html.find('.spinner-border').remove();
      }

      $f[0]._saved_status=$html.html();
      $f.find('.form-saved-status').html($html.html());
      $('.form-saved-status-global').html($html.html());
    }

  },

  //cleanup any exisitng form errors
  clean_form_errors: function ($form) {
    $form=$($form);
    $form.find('.has-danger').removeClass('has-danger');
    $form.find('.is-invalid').removeClass('is-invalid');
    $form.find('[class^="err-"]').removeClass('invalid-feedback');
  },

  //form - optional, if set - just this form processed
  //err_json - optional, if set - this error json used instead of form's data-errors
  process_form_errors: function (form, err_json) {
    //console.log(form, err_json);
    var selector= 'form[data-errors]';
    if (form) selector=$(form);
    $(selector).each(function (i, el) {
      var $f = $(el);
      var errors = err_json ? err_json : $f.data('errors');
      if (errors) console.log(errors);
      if ($.isPlainObject(errors)){
        //highlight error fields
        $.each(errors,function(key, errcode) {
          var $input = $f.find('[name="item['+key+']"],[name="'+key+'"]');
          if ($input.length){
            var $p=$input.parent();
            if ($p.is('.input-group,.custom-control,.dropdown,.twitter-typeahead')) $p = $p.parent();
            if (!$p.closest('form, table').is('table')){//does not apply to inputs in subtables
              $input.closest('.form-group, .form-row').not('.noerr').addClass('has-danger'); //highlight whole row (unless .noerr exists)
            }
            $input.addClass('is-invalid'); //mark input itself
            $input.parent('.input-group,.dropdown,.twitter-typeahead').addClass('is-invalid'); //mark input group container
            if (errcode!==true && errcode.length){
              $p.find('.err-'+errcode).addClass('invalid-feedback'); //find/show specific error message
            }
          }
        });
      }
    });
  },

  /* structure:
    <div class="file-drop-area">
        <span class="fake-btn">Choose files or drag and drop your files here</span>
        <input class="d-none" type="file" multiple >
    </div>
  */
  setup_file_drop_area: function (){
    document.querySelectorAll('.file-drop-area').forEach(dropArea => {
        const fileInput = dropArea.querySelector('input[type="file"]');
        const fakeBtn = dropArea.querySelector('.fake-btn');

        fakeBtn.onclick = () => fileInput.click();
        fileInput.onchange = () => {
            fakeBtn.innerHTML = fileInput.files.length + ' file(s) selected';
        };

        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            dropArea.addEventListener(eventName, (e) => {
                e.preventDefault();
                e.stopPropagation();
                if (eventName === 'dragenter' || eventName === 'dragover') {
                    dropArea.classList.add('highlight');
                } else {
                    dropArea.classList.remove('highlight');
                }
            }, false);
        });

        dropArea.ondrop = (e) => {
            let files = e.dataTransfer.files;
            fileInput.files = files;
            fileInput.onchange();
        };
    });
  },

  delete_btn: function (ael){
    fw.confirm('<strong>ARE YOU SURE</strong> to delete this item?', function(){
      var XSS=$(ael).parents('form:first').find("input[name=XSS]").val();
      var action = ael.href+ ( ( ael.href.match(/\?/) ) ? '&':'?' ) +'XSS='+XSS;
      $('#FOneDelete').attr('action', action).submit();
    });
    return false;
  },

  // if no data-filter defined, tries to find first form with data-list-filter
  // <table class="list" data-rowtitle="Double click to Edit" [data-rowtitle-type="explicit"] [data-filter="#FFilter"]>
  //  <thead>
  //    <tr data-sortby="" data-sortdir="asc|desc"
  //  ... <tr data-url="url to go on double click">
  //       <td data-rowtitle="override title on particular cell if 'explicit' set above">
  make_table_list: function(tbl){
    var $tbl=$(tbl);
    if (!$tbl.length) return; //skip if no list tables found

    var $f = $tbl.data('filter') ? $($tbl.data('filter')) : $('form[data-list-filter]:first');

    $tbl.on('dblclick', 'tbody tr', function(e){
      if ($(e.target).is('input.multicb')) return;
      var url=$(this).data('url');
      if (url) window.location=url;
    });

    var rowtitle=$tbl.data('rowtitle');
    if (typeof(rowtitle)=='undefined') rowtitle='Double click to Edit';
    var title_selector = "tbody tr";
    if ($tbl.data('rowtitle-type')=='explicit') title_selector="tbody tr td.rowtitle";
    $tbl.find(title_selector).attr('title', rowtitle);

    var $sh=$tbl.find('tr[data-sortby]');
    var sortby=$sh.data('sortby');
    var sortdir=$sh.data('sortdir');

    var sort_img= (sortdir=='desc') ? fw.ICON_SORT_DESC : fw.ICON_SORT_ASC;
    var $th = $sh.find('th[data-sort="'+sortby+'"]').addClass('active-sort');
    var $thcont = !$tbl.is('.table-dense') && $th.find('div').length>0 ? $th.find('div') : $th;
    $thcont.append('<span class="ms-1">'+sort_img+'</span>');

    $sh.on('click', 'th[data-sort]', function() {
      var $td=$(this);
      var sortdir=$sh.data('sortdir');
      //console.log(sortdir, $td.is('.active'));

      if ( $td.is('.active-sort') ){
        //active column - change sort dir
        sortdir = (sortdir=='desc') ? 'asc' : 'desc';
      }else{
        //new collumn - set sortdir to default
        sortdir='asc';
      }

      if (!$f) return; //skip if no filter form
      $('input[name="f[sortdir]"]', $f).val(sortdir);
      $('input[name="f[sortby]"]', $f).val( $td.data('sort') );

      $f.submit();
    });

    //make table header freeze if scrolled too far below
    $(window).on('resize, scroll', this.debounce(function() {
        fw.apply_scrollable_table($tbl);
      }, 10));
  },

  //make table in pane scrollable with fixed header
  //if scrollable header exists - just recalc positions (i.e. if called from window resize)
  //requires css  .data-header, .data-header table
  apply_scrollable_table: function ($table, is_force) {
      $table = $($table);
      if (!$table.length) return;//skip if no scrollable table defined

      var $scrollable = $table.closest('.scrollable');
      if (!$scrollable.length) {
          $table.wrap('<div class="scrollable">');
          $scrollable = $table.closest('.scrollable');
      }

      var $dh = $scrollable.find('.data-header');
      var to = $scrollable.offset();
      var win_scrollY= window.pageYOffset || document.documentElement.scrollTop;
      if (win_scrollY<to.top) {
          //no need to show fixed header
          $dh.remove();
          $table.find('thead').css({visibility: ''});
          return;
      }

      if (!$dh.length || is_force){
          $dh.remove();

          //create fixed header for the table
          var $th_orig = $table.find('thead:first');
          $th_orig.css({visibility: 'hidden'});

          var $th = $table.find('thead:first').clone(true);
          //$th.find('tr').not(':eq(0)').remove(); //leave just first tr
          $th.css({visibility: ''});

          var $htable = $('<table></table>').width($table.width()).append( $th );
          $htable[0].className = $table[0].className; //apply all classes
          $htable.removeClass('data-table');

          var $th0 = $table.find('thead:first > tr > th');
          var $thh = $htable.find('thead:first > tr > th');
          $th0.each(function(i,el) {
              $thh.eq(i).outerWidth( $(this).outerWidth() );
          });

          $dh = $('<div class="data-header"></div>').append($htable).css({
              top: 0,
              left: to.left-window.scrollX
          });

          $scrollable.append($dh);

      }else{
          //just adjust the header position
          $dh.css({
              left: to.left-window.scrollX
          });
      }
  },

  //optional, requires explicit call in onload.js
  // <a href="#" data-mail-name="NAME" data-mail-domain="DOM" data-mail="subject=XXX">[yyy]</a>
  setup_mailto: function (){
    $('a[data-mail-name]').each(function(index, el) {
      var $el = $(el);
      var name=$el.data('mail-name');
      if (name>''){
        var dom=$el.data('mail-domain');
        var more=$el.data('mail');

        el.href='mailto:'+name+'@'+dom+((more>'')?'?'+more:'');
        if ( $el.text()==='' ){
            $el.text(name+'@'+dom);
        }
      }else{
        $el.text('');
      }
    });
  },

  title2url: function (from_input, to_input){
      var title=$(from_input).val();
      title=title.toLowerCase();
      title=title.replace(/^\W+/,'');
      title=title.replace(/\W+$/,'');
      title=title.replace(/\W+/g,'-');
      $(to_input).val(title);
  },

  field_insert_at_cursor: function (myField, myValue) {
      //IE support
      if (document.selection) {
          myField.focus();
          sel = document.selection.createRange();
          sel.text = myValue;
      }
      //MOZILLA and others
      else if (myField.selectionStart || myField.selectionStart == '0') {
          var startPos = myField.selectionStart;
          var endPos = myField.selectionEnd;
          myField.value = myField.value.substring(0, startPos) + myValue + myField.value.substring(endPos, myField.value.length);
          myField.selectionStart = startPos + myValue.length; myField.selectionEnd = startPos + myValue.length;
      } else {
          myField.value += myValue;
      }
  },

  ajaxify_list_navigation: function (div_nav, onclick){
    $(div_nav).on('click', 'a', function(e){
      e.preventDefault();
      onclick(this);
    });
  },

  // password stength indicator
  // usage: $('#pwd').on('blur change keyup', fw.renderPwdBar);
  renderPwdBar: function(e) {
      var $this = $(this);
      var pwd = $this.val();
      var score = fw.scorePwd(pwd);
      var wbar = parseInt(score*100/120); //over 120 is max
      if (pwd.length>0 && wbar<10) wbar=10; //to show "bad"
      if (wbar>100) wbar=100;

      var $pr = $this.parent().find('.progress');
      if (!$pr.length){
          $pr = $('<div class="progress mt-1"><div class="progress-bar" role="progressbar" style="width: 0%" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100"></div></div>').appendTo($this.parent());
      }
      var $bar = $pr.find('.progress-bar');
      $bar.css('width', wbar+'%');
      $bar.removeClass('bg-danger bg-warning bg-success bg-dark').addClass(fw.scorePwdClass(score))
      $bar.text(fw.scorePwdText(score))
      //console.log(pwd, score,'  ', wbar+'%');
  },

  scorePwd: function(pwd) {
      var result = 0;
      if (!pwd) return result;

      // award every unique letter until 5 repetitions
      var chars = {};
      for (var i=0; i<pwd.length; i++) {
          chars[pwd[i]] = (chars[pwd[i]] || 0) + 1;
          result += 5.0 / chars[pwd[i]];
      }

      // bonus points for mixing it up
      var vars = {
          digits: /\d/.test(pwd),
          lower: /[a-z]/.test(pwd),
          upper: /[A-Z]/.test(pwd),
          other: /\W/.test(pwd),
      }
      var ctr = 0;
      for (var k in vars) {
          ctr += (vars[k] == true) ? 1 : 0;
      }
      result += (ctr - 1) * 10;

      //adjust for length
      result = (Math.log(pwd.length) / Math.log(8))*result

      return result;
  },

  scorePwdClass: function(score) {
      if (score > 100) return "bg-dark";
      if (score > 60) return "bg-success";
      if (score >= 30) return "bg-warning";
      return "bg-danger";
  },

  scorePwdText: function(score) {
      if (score > 100) return "strong";
      if (score > 60) return "good";
      if (score >= 30) return "weak";
      return "bad";
  }

};
