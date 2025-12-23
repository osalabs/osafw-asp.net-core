/*
  misc client utils for the osafw framework
  www.osalabs.com/osafw
  (c) 2009-2024 Oleg Savchuk www.osalabs.com
*/

window.fw={
  HTML_LOADING: '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Loading...',
  HTML_SPINNER_CT: '<span class="fw-spinner-container"> <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span></span',
  HTML_SPINNER_SM: '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>',
  ICON_SORT_ASC: '<svg width="1em" height="1em" viewBox="0 0 16 16" class="bi bi-arrow-down" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M8 1a.5.5 0 0 1 .5.5v11.793l3.146-3.147a.5.5 0 0 1 .708.708l-4 4a.5.5 0 0 1-.708 0l-4-4a.5.5 0 0 1 .708-.708L7.5 13.293V1.5A.5.5 0 0 1 8 1z"/></svg>',
  ICON_SORT_DESC: '<svg width="1em" height="1em" viewBox="0 0 16 16" class="bi bi-arrow-up" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M8 15a.5.5 0 0 0 .5-.5V2.707l3.146 3.147a.5.5 0 0 0 .708-.708l-4-4a.5.5 0 0 0-.708 0l-4 4a.5.5 0 1 0 .708.708L7.5 2.707V14.5a.5.5 0 0 0 .5.5z"/></svg>',
  PWD_PROGRESS_BAR: '<div class="progress mt-1"><div class="progress-bar" role="progressbar" style="width: 0%" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100"></div></div>',
  // messages
  MSG_UNSAVED_CHANGES: 'There are unsaved changes.\nDo you really want to Cancel editing the Form?',
  MSG_UNSAVED_CHANGES_CONFIRM: '<strong>There are unsaved changes.</strong><br>Do you really want to Cancel editing the Form?',
  MSG_AUTOSAVE_ERROR: 'Auto-save error. Server error occurred. Try again later.',
  MSG_UPLOAD_FAILED: 'Upload failed',
  MSG_DELETE_CONFIRM: '<strong>ARE YOU SURE</strong> to delete this item?',
  autosave: {
    statusElSelector: '.fw-autosave-status',
    lastSaved: null,
    setStatus: function (state, options) {
      var $els = $(this.statusElSelector);
      if (!$els.length) return;

      options = options || {};

      var label = '';
      var prefix = '';
      switch (state) {
        case 'enabled':
          label = 'Autosave enabled';
          break;
        case 'saving':
          label = 'Saving...';
          prefix = fw.HTML_SPINNER_SM;
          break;
        case 'saved':
          var ts = options.timestamp instanceof Date ? options.timestamp : new Date();
          this.lastSaved = ts;
          var timeStr = options.showTime === false ? '' : this.formatTime(ts);
          label = timeStr ? 'Saved ' + timeStr : 'Saved';
          break;
        case 'error':
          label = options.message || 'Autosave failed';
          break;
        case 'changed':
          label = 'Unsaved changes';
          break;
        default:
          label = state;
      }

      $els.html((prefix ? prefix + ' ' : '') + label);
    },
    formatTime: function (date) {
      try {
        return date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
      } catch (e) {
        return '';
      }
    }
  },

  // Unified helper: updates saved/unsaved status and optional progress spinner
  // is_changed: true/false updates internal changed flag, pass undefined to keep previous value
  // is_progress: true - show spinner, false - hide spinner, undefined - keep previous spinner state
  set_form_saved_status: function ($form, is_changed, is_progress) {
    var $f = $($form);
    if (!$f.length) return;

    // update changed flag if explicitly passed
    if (typeof is_changed === 'boolean') {
      $f.data('is-changed', is_changed);
    }
    var changed = $f.data('is-changed') === true;

    // determine spinner state
    if (typeof is_progress === 'boolean') {
      $f.data('is-progress', is_progress);
    }
    var isProgress = $f.data('is-progress') === true;

    var cls = changed ? 'bg-danger' : 'bg-success';
    var txt = changed ? 'not saved' : 'saved';
    var html = (isProgress ? fw.HTML_SPINNER_SM : '') + ' <span class="badge ' + cls + '">' + txt + '</span>';

    $f.find('.form-saved-status').html(html);
    $('.form-saved-status-global').html(html);

    var statusState = null;
    if (isProgress)
      statusState = 'saving';
    else if (is_changed === true || changed)
      statusState = 'changed';
    else if (is_changed === false || !changed)
      statusState = 'saved';

    if (statusState)
      fw.autosave.setStatus(statusState, { timestamp: new Date() });
  },

  // requires https://github.com/osalabs/bootstrap-toaster
  ok: function (str, options){
    options = $.extend({}, options);
    ToastSuccess(str, options);
  },

  error: function (str, options){
    options = $.extend({}, options);
    ToastDanger(str, options);
  },

  // requires https://github.com/osalabs/bootstrap-alert-confirm-prompt
  // usage: fw.alert('Process completed','Worker');
  alert: function (content, title){
    alert(content, {title: title});
  },

  /*
  fw.confirm('Are you sure?', 'optional title', function(){
    //proceed OK answer
  });
  */
  confirm: function (content, title_or_cb, callback){
    let options={};
    if ($.isFunction(title_or_cb)){
      callback=title_or_cb;
    }else{
      options.title=title_or_cb;
    }

    confirm(content, options).then(result => {if (result) callback();});
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
          ToastInfo("WORD to search for contains word<br>"+
            "!WORD to search for NOT contains word<br>"+
            "=WORD to search for equals word<br>"+
            "!=WORD to search for NOT equals word<br>"+
            "&lt;=N, &lt;N, &gt;=N, &gt;N - compare numbers",
            {header: 'Search hints', html: true, autohide: false});
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
        $(this.form).trigger('submit');
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
    $(document).on('click', 'table.list:not([data-row-selectable="false"]) > tbody > tr', function (e) {
      // Do not process on text selection
      if (window.getSelection().toString() !== '') return;

      const $this = $(this);
      const $target = $(e.target);

      // Check if the clicked element or any of its parents is an interactive element
      if ($target.closest('a, button, input, select, textarea').length) {
        return; // Do not process if an interactive element was clicked
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
    fw.setup_att_files_upload();

    $(document).on('change', '.on-refresh', function (e) {
      var $this = $(this);
      var $f = $this.closest('form');
      $f.find('input[name=refresh]').val($this.attr('id') || $this.attr('name') || 1);
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

    //on click - submit via POST with a spinner on clicked element
    //  with optional confirmation (if title set)
    //  optionally via ajax
    //  optionally replace target content (otherwise fw.ok(json.message) displayed)
    //  optionally show spinner
    //ex: <button type="button" class="btn btn-default on-fw-submit" data-url="SUBMIT_URL?XSS=<~SESSION[XSS]>" data-title="CONFIRMATION TITLE" data-ajax data-target="#optional" data-spinner>Button</button>
    $(document).on('click', '.on-fw-submit', function (e){
      e.preventDefault();
      var $this = $(this);
      var url = $this.data('url');
      var title = $this.data('title');
      var ajax = $this.is('[data-ajax]');
      var target = $this.data('target');
      var spinner = $this.is('[data-spinner]');

      if (title) {
        fw.confirm(title, function() {
          fw.submit($this, url, ajax, target, spinner);
        });
      } else {
        fw.submit($this, url, ajax, target, spinner);
      }
    });

    fw.textarea_autoresize('textarea.autoresize');
  },

  //submit url via POST form
  submit: function ($el, url, is_ajax, target, is_spinner) {
    var $form = $('<form action="' + url + '" method="post"></form>');

    if (is_spinner){
      var spinner = $(fw.HTML_SPINNER_CT);
      $el.prop('disabled', true).append(spinner); // Add spinner and disable button
    }

    var complete = function() {
      if (is_spinner){
        $el.prop('disabled', false).find('.fw-spinner-container').remove(); // Remove spinner and enable button
      }
    };

    if (is_ajax) {
      var options = {
        complete: complete // Ensure spinner is removed after the request completes
      };

      if (target) {
        options.target = target;
      } else {
        options.dataType = 'json';
        options.success = function(response) {
          fw.ok(response.message ?? 'Success'); // Display message using fw.ok
        };
        options.error = function(xhr, status, error) {
          fw.error("An error occurred: " + error); // Error handling
        };
      }

      $form.ajaxSubmit(options);
    } else {
      $form.appendTo('body').submit();//non-ajax submit
    }
  },

  //automatically resize textarea element to it's content (but no downsize smaller than initial)
  textarea_autoresize: function(selector){
    const textareas = document.querySelectorAll(selector);

    textareas.forEach(textarea => {
        const initialHeight = textarea.scrollHeight;

        function resizeTextarea() {
            // Reset textarea height to initial height to recalculate scroll height correctly
            textarea.style.height = `${initialHeight}px`;

            // Set the height to the scroll height, ensuring it doesn't go below initial height
            const newHeight = Math.max(initialHeight, textarea.scrollHeight);
            textarea.style.height = `${newHeight}px`;
        }

        textarea.addEventListener('input', resizeTextarea);
        resizeTextarea(); // Initialize the correct height
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
          return fw.MSG_UNSAVED_CHANGES;
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
      fw.confirm(fw.MSG_UNSAVED_CHANGES_CONFIRM, function (){
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

    fw.autosave.setStatus('enabled');

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
      fw.set_form_saved_status($f, true); // mark changed

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
      fw.set_form_saved_status($f, undefined, false); // hide spinner only
      $f.data('is-ajaxsubmit', false);
    }

    function form_handle_errors($f, data, hint_options){
        if (data.error?.details) {
            //auto-save error - highlight errors
            fw.process_form_errors($f, data.error?.details);
        }
        fw.error(data.error?.message || fw.MSG_AUTOSAVE_ERROR, hint_options);
        fw.autosave.setStatus('error', { message: data.error?.message || fw.MSG_AUTOSAVE_ERROR });
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
      fw.set_form_saved_status($f, undefined, true); // show spinner
      $f.ajaxSubmit({
          dataType: 'json',
          success: function (data) {
              form_reset_state($f);
              //console.log('ajaxSubmit success', data);
              $('#fw-form-msg').hide();
              fw.clean_form_errors($f);
              if (!data.error) {
                  fw.set_form_saved_status($f, false); // saved
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
    <div class="fw-file-drop-area">
        <span class="fake-btn">Choose files or drag and drop your files here</span>
        <input class="d-none" type="file" multiple >
    </div>
  */
  setup_file_drop_area: function (){
    document.querySelectorAll('.fw-file-drop-area').forEach(dropArea => {
        const fileInput = dropArea.querySelector('input[type="file"]');
        const fakeBtn = dropArea.querySelector('.fake-btn');

        fakeBtn.onclick = () => fileInput.click();
        //fileInput.onchange = () => {
        //    fakeBtn.innerHTML = fileInput.files.length + ' file(s) selected';
        //};

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
            fileInput.dispatchEvent(new Event('change', { bubbles: true }))
        };
    });
  },

  setup_att_files_upload: function(){
    $(document).on('change', '.fw-file-drop-area input[type=file]', function(e){
      var $input = $(this);
      var $drop = $input.closest('.fw-file-drop-area');
      var files = Array.from(this.files || []); // making copy as array as below we clear input
      var $form = $drop.closest('form');
      if (!files.length) return;

      this.value = null; //cleanup files input

      var item_id = $drop.data('item-id');
      var upload_url = $drop.data('upload-url');
      var att_category = $drop.data('att-category') || ''; // optional
      var att_post_prefix = $drop.data('att-post-prefix') || ''; // should be set to related field name from config.json      
      var fwentity = $drop.data('fwentity') || '';
      if (!item_id) { 
          fw.alert('Save record first');
          $input.val('');
          return;
      }

      var $list = $drop.next('.att-list');
      files.forEach(function(file){
        var $item = $list.find('.tpl').clone().removeClass('tpl d-none');
        $item.find('.att-iname').text(file.name);
        $item.find('.att-size').text('('+fw.bytes2str(file.size)+')');
        var $progress = $item.find('.progress-bar');
        $list.append($item);

        var fd = new FormData();
        fd.append('file1', file);
        fd.append('XSS', $input.closest('form').find('input[name=XSS]').val());
        if (att_category) fd.append('item[att_category]', att_category);
        if (fwentity) fd.append('item[fwentity]', fwentity);
        fd.append('item[item_id]', item_id);

        $.ajax({
          url: upload_url,
          type: 'POST',
          data: fd,
          processData: false,
          contentType: false,
          dataType: 'json',
          headers: { 'Accept': 'application/json' },

          xhr: function(){
            var xhr = new window.XMLHttpRequest();
            xhr.upload.addEventListener('progress', function(evt){
              if (evt.lengthComputable){
                var percent = evt.loaded / evt.total * 100;
                $progress.css('width', percent+'%');
              }
            }, false);
            return xhr;
          },
          success: function(res){
            $item.find('.progress').remove();
            if (res && !res.error){
              $item.find('.att-iname').attr("href", res.url).text(res.iname);
              $item.find('.att-post-prefix').attr("name", att_post_prefix + '[' + res.id + ']').val(1);
              //$drop.closest('form').trigger('autosave');
              if ($form.length){
                fw.set_form_saved_status($form, false);
              }
            }else{
              $item.remove();
              fw.error(data.error?.message || fw.MSG_UPLOAD_FAILED);
            }
            $input.val('');
          },
          error: function(){
            $item.remove();
            fw.error(fw.MSG_UPLOAD_FAILED);
            $input.val('');
          }
        });

      });
    });

    $(document).on('click', '.on-remove-att', function(){
      var $item=$(this).closest('.att-item');
      var $form=$(this).closest('form');
      $item.remove();
      $form.trigger('autosave');
    });
  },

  delete_btn: function (ael){
    fw.confirm(fw.MSG_DELETE_CONFIRM, function(){
      $('#FOneDelete').attr('action', ael.href).submit();
    });
    return false;
  },

  // if no data-filter defined, tries to find first form with data-list-filter
  // <table class="list" data-rowtitle="Double click to Edit" [data-rowtitle-type="explicit"] [data-filter="#FFilter"] [data-row-selectable="false"]>
  //  <thead>
  //    <tr data-sortby="" data-sortdir="asc|desc"
  //  ... <tr data-url="url to go on double click">
  //       <td data-rowtitle="override title on particular cell if 'explicit' set above">
  make_table_list: function(tbl){
    var $tbl=$(tbl);
    if (!$tbl.length) return; //skip if no list tables found

    var $f = $tbl.data('filter') ? $($tbl.data('filter')) : $('form[data-list-filter]:first');
    var is_selectable = !$tbl.is('[data-row-selectable="false"]');
    
    $tbl.on('dblclick', 'tbody tr', function(e){
      var $target = $(e.target);
      // Do not process on text selection, but only if clicked on the element with a selected text (double click on th/td padding selects all text inside the cell)
      if (window.getSelection().toString() !== '' && !$target.is('th, td')) return;

      if ($target.is('input.multicb')) return;
      var url=$(this).data('url');
      if (url) window.location=url;
    });

    var rowtitle=$tbl.data('rowtitle');
    if (typeof(rowtitle)=='undefined') rowtitle=(is_selectable ? 'Click to select, ' : '') + 'Double click to Edit';
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
          myField.selectionStart = startPos + myValue.length;
          myField.selectionEnd = startPos + myValue.length;
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
          $pr = $(fw.PWD_PROGRESS_BAR).appendTo($this.parent());
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
  },

  bytes2str: function(bytes){
    var units=['B','KiB','MiB','GiB','TiB'];
    var i=0;
    while(bytes>=1024 && i<units.length-1){bytes/=1024;i++;}
    return Math.round(bytes*10)/10+' '+units[i];
  },

};
