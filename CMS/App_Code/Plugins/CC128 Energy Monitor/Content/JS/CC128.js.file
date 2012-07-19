function ajax(url, method, success, failure)
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
    a.send();
}
// Credit: http://www.gotknowhow.com/articles/how-to-get-the-base-url-with-javascript
function getBaseURL()
{
    var url = location.href;
    return url.substring(0, url.indexOf('/', 14));
}
function cc128onLoad()
{
    setInterval(
    function ()
    {
        ajax(getBaseURL() + "/power/ajax", "GET",
        function (a)
        {
            var wattsCur = a.responseXML.getElementsByTagName("d")[0].getElementsByTagName("w")[0].childNodes[0].nodeValue;
            var wattsMax = a.responseXML.getElementsByTagName("d")[0].getElementsByTagName("m")[0].childNodes[0].nodeValue;
            document.getElementById("BAR").style.height = (wattsCur != 0 && wattsMax != 0 ? wattsCur / wattsMax * 100 : 0) + "%";
            document.getElementById("USAGE").innerHTML = wattsCur;
            document.getElementById("UPDATED").innerHTML = new Date().toString();
        },
        function (a)
        {
            document.getElementById("BAR").style.height = "0%";
            document.getElementById("USAGE").innerHTML = "XXXX";
            document.getElementById("UPDATED").innerHTML = "failed at " + new Date().toString();
        }
        );
    }, 1000);
}