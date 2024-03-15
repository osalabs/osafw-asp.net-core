function highlightSQL() {
    $('.sql-code').each(function(i, el){
       var sqlCode = $(this).text();
       // Replace text with highlighted spans
       sqlCode = sqlCode.replace(/\b(SELECT|FROM|WHERE|AND|OR|INSERT|INTO|VALUES|UPDATE|SET|DELETE|ALTER|TABLE|ADD|REMOVE)\b/gi, '<span class="sql-keyword">$&</span>');
       sqlCode = sqlCode.replace(/'([^']+)'/g, '<span class="sql-string">$&</span>');
       sqlCode = sqlCode.replace(/(--[^\n]*)/g, '<span class="sql-comment">$&</span>');
       
       $(this).html(sqlCode);
    });
}