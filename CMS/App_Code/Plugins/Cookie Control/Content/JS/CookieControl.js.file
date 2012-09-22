function cookieControlToggle()
{
    var enabled = false;
    var cs = document.cookie.split(';');
    var name;
    for (var i = 0; i < cs.length; i++)
    {
        if (cs[i].split('=')[0] == "cookie-control")
        {
            enabled = true;
            break;
        }
    }
    if (enabled)
    {
        var expires = new Date();
        expires.setTime(expires.getTime() - 1);
        document.cookie = "cookie-control=1; expires=" + expires.toGMTString();
    }
    else
        document.cookie = "cookie-control=1";
    document.getElementById("cookie-control-toggle").src = getBaseURL() + "/Content/Images/cookiecontrol/toggle_" + (enabled ? "off" : "on") + ".png";

    return false;
}
// Credit: http://www.gotknowhow.com/articles/how-to-get-the-base-url-with-javascript
function getBaseURL()
{
    var url = location.href;
    return url.substring(0, url.indexOf('/', 14));
}