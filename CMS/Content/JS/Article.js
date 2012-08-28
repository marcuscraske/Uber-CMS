function ajax(url, method, success, failure, send)
{
    var a;
    if (window.XMLHttpRequest) a = new XMLHttpRequest();
    else a = new ActiveXObject("Microsoft.XMLHTTP");
    a.onreadystatechange = function ()
    {
        if (a.readyState == 4)
            if (a.status == 200) success(a);
            else failure(a);
    }
    a.open(method, url, true);
    if(send) a.send();
    return a;
}
function previewArticle()
{
    var text = document.getElementById("ARTICLE_BODY");
    var output = document.getElementById("ARTICLE_PREVIEW");
    var allowHtml = document.getElementById("allow_html").checked;
    var request = ajax(getBaseURL() + "/article/preview", "POST",
    function (a)
    {
        output.innerHTML = a.responseText;
    },
    function (a)
    {
        output.innerHTML = "Failed to preview article, an error occurred; are you connected to the site? Please try again...";
    },
    false);
    request.setRequestHeader("Content-Type", "application/x-www-form-urlencoded");
    request.send("allow_html=" + (allowHtml ? "1" : "0") + "&data=" + encodeURIComponent(text.value));
}
function removeProtection(container)
{
    container.innerHTML = container.innerHTML.replace(/&lt;/g, "<").replace(/&gt;/g, ">");
    container.removeAttribute("onclick");
    container.removeAttribute("class");
}
function selectText(control)
{
    control.focus();
    control.select();
}
// Credit: http://www.gotknowhow.com/articles/how-to-get-the-base-url-with-javascript
function getBaseURL()
{
    var url = location.href;
    return url.substring(0, url.indexOf('/', 14));
}