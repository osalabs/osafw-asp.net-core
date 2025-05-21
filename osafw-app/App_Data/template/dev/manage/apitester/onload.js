$('.form-check-input[name=method]').change(function() {
    var $this = $('.form-check-input[name=method]:checked');
    if ($this.val() == 'POST' || $this.val() == 'PUT' || $this.val() == 'PATCH') {
        $('#uploadSection').show();
    } else {
        $('#uploadSection').hide();
    }
});

$('#apiForm').submit(function(event) {
    event.preventDefault();

    var authKey = $('#authKey').val();
    var host = $('#host').val();
    var url = $('#url').val();
    var method = $('.form-check-input[name=method]:checked').val();
    var query = $('#query').val();
    var contentType = $('#contentType').val();
    var jsonBody = $('#jsonBody').val();

    var apiUrl = host + url;
    if (query) {
        apiUrl += '?' + query;
    }

    var headers = {};
    if (authKey) {
        headers['Authorization'] = 'Bearer ' + authKey;
    }

    var ajaxOptions = {
        dataType: 'json',
        url: apiUrl,
        method: method,
        headers: headers,
        success: function(data, textStatus, xhr) {
            $('#responseSection').show();
            $('#responseStatus').text('Status: ' + xhr.status + ' ' + xhr.statusText);
            $('#responseBody').text(JSON.stringify(data, null, 2));
        },
        error: function(xhr, textStatus, errorThrown) {
            $('#responseSection').show();
            $('#responseStatus').text('Status: ' + xhr.status + ' ' + xhr.statusText);
            if (xhr.responseJSON){
                $('#responseBody').text(JSON.stringify(xhr.responseJSON, null, 2));
            }else{
                $('#responseBody').text(xhr.responseText);
            }            
        }
    };

    if (method === 'POST' || method === 'PUT' || method === 'PATCH') {
        if (contentType === 'application/json') {
            ajaxOptions.contentType = 'application/json';
            ajaxOptions.data = jsonBody;
        } else if (contentType === 'multipart/form-data') {
            var formData = new FormData();
            var fileInput = document.getElementById('fileUpload');
            if (fileInput.files.length > 0) {
                formData.append('file', fileInput.files[0]);
            }
            ajaxOptions.processData = false;
            ajaxOptions.contentType = false;
            ajaxOptions.data = formData;
        }
    }

    $.ajax(ajaxOptions);
});