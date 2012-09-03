/*
* UBERMEAT FOSS
* ****************************************************************************************
* License:                 Creative Commons Attribution-ShareAlike 3.0 unported
*                          http://creativecommons.org/licenses/by-sa/3.0/
* 
* Project:                 Uber Media
* File:                    /Content/JS/Core.js
* Author(s):               limpygnome						limpygnome@gmail.com
* To-do/bugs:              none
* 
* Shared JavaScript code used for the client-side of the website i.e. Ajax etc.
*/
function Ajax(url, method, success, failure)
{
    var a;
    if (window.XMLHttpRequest) a = new XMLHttpRequest();
    else a = new ActiveXObject("Microsoft.XMLHTTP");
    a.onreadystatechange = function () {
        if (a.readyState == 4)
            if(a.status == 200) success(a);
            else failure(a);
    }
    a.open(method, url, true);
    a.send();
}
var searchCount = 0;
var contentSet = true;
var searchPrevContent = null;
function Search(input)
{
    if (contentSet)
    {
        searchPrevContent = document.getElementById("RIGHT").innerHTML;
        contentSet = false;
    }
    else if (input.length == 0)
    {
        contentSet = true;
        document.getElementById("RIGHT").innerHTML = searchPrevContent;
        searchPrevContent = null;
    }
    else
    {
        var localSearchCount = ++searchCount;
        Ajax(getBaseURL() + "/ubermedia/search?q=" + encodeURI(input), "GET",
        function (a)
        {
            if (localSearchCount == searchCount) // The response may be slower than another future response and hence this avoids overlapping
                document.getElementById("RIGHT").innerHTML = a.responseText;
        },
        function (a)
        {
            alert("Failed to handle search request...");
        });
    }
}
function ChangeMediaComputer(value)
{
    Ajax(getBaseURL() + "/ubermedia/change?c=" + encodeURI(value), "GET",
    function (a)
    {
        if (a.responseText.length > 0) alert(a.responseText);
    },
    function (a)
    {
        alert("Could not change selected media computer, check you have a connection to the website and the media computer still exists!");
    });
}
// Credit: http://www.gotknowhow.com/articles/how-to-get-the-base-url-with-javascript
function getBaseURL()
{
    var url = location.href;
    return url.substring(0, url.indexOf('/', 14));
}
// Grabs status updates
function controlsInit(mediacomputer)
{
    setInterval(function () { controlsUpdate(mediacomputer); }, 500);
}
var slidersGripped = false;
var playlistHash = "";
var currentItem = "";
function grip(val)
{
    slidersGripped = val;
}
function controlsUpdate(mediacomputer)
{
    Ajax(getBaseURL() + "/ubermedia/control?cmd=status&mc=" + encodeURI(mediacomputer), "GET",
    function (a)
    {
        // Parse the status data
        var doc = a.responseXML.getElementsByTagName("d")[0];
        var position = doc.getElementsByTagName("p")[0].childNodes.length > 0 ? doc.getElementsByTagName("p")[0].childNodes[0].nodeValue : "0";
        var duration = doc.getElementsByTagName("d")[0].childNodes.length > 0 ? doc.getElementsByTagName("d")[0].childNodes[0].nodeValue : "0";
        var volume = doc.getElementsByTagName("v")[0].childNodes.length > 0 ? doc.getElementsByTagName("v")[0].childNodes[0].nodeValue : "0";
        var muted = doc.getElementsByTagName("m")[0].childNodes.length > 0 ? doc.getElementsByTagName("m")[0].childNodes[0].nodeValue : "1";
        var status = doc.getElementsByTagName("s")[0].childNodes.length > 0 ? doc.getElementsByTagName("s")[0].childNodes[0].nodeValue : "7"; // 7 = error
        var pv = doc.getElementsByTagName("pv")[0].childNodes.length > 0 ? doc.getElementsByTagName("pv")[0].childNodes[0].nodeValue : "";
        var ph = doc.getElementsByTagName("ph")[0].childNodes.length > 0 ? doc.getElementsByTagName("ph")[0].childNodes[0].nodeValue : "";

        if (!slidersGripped) // Only set the sliders if they're not being gripped by the user
        {
            // Set position slider
            document.getElementById("POSITION").setAttribute("max", duration);
            document.getElementById("POSITION").value = position;
            // Set volume slider
            document.getElementById("VOLUME").value = volume;
        }
        // Set play toggle button
        if (status == "1") // 1 = playing
            document.getElementById("PLAY").className = "B BPAUSE";
        else document.getElementById("PLAY").className = "B BPLAY";
        // Set mute button
        if (muted == "1") document.getElementById("MUTE").className = "MUTE";
        else document.getElementById("MUTE").className = "UNMUTE";
        // Set time text
        document.getElementById("TIME").innerHTML = timeString(position) + "/" + timeString(duration);
        // Check if the playlist needs updating
        if (ph != playlistHash || pv != currentItem)
        {
            playlistHash = ph;
            controlsUpdatePlaylist(mediacomputer);
        }
    },
    function (a)
    {
    });
}
function controlsUpdatePlaylist(mediacomputer)
{
    Ajax(getBaseURL() + "/ubermedia/control?mc=" + mediacomputer + "&cmd=playlist", "GET",
    function (a)
    {
        var doc = a.responseXML.getElementsByTagName("d")[0];
        var current = doc.getElementsByTagName("c")[0];
        var upcoming = doc.getElementsByTagName("u")[0];
        // Set the current item
        if (current.getElementsByTagName("v")[0].childNodes.length == 0)
        {
            document.getElementById("PLAYING").innerHTML = "None.";
            currentItem = "";
        }
        else
        {
            document.getElementById("PLAYING").innerHTML = buildPlaylistItem("", current.getElementsByTagName("v")[0].childNodes[0].nodeValue, current.getElementsByTagName("t")[0].childNodes[0].nodeValue, mediacomputer);
            currentItem = current.getElementsByTagName("v")[0].childNodes[0].nodeValue;
        }
        // Rebuild upcoming items
        if (upcoming.getElementsByTagName("i").length == 0)
            document.getElementById("UPCOMING").innerHTML = "None.";
        else
        {
            var data = "";
            var items = upcoming.getElementsByTagName("i");
            var item;
            for (var i = 0; i < items.length; i++)
            {
                item = items[i];
                data += buildPlaylistItem(item.getElementsByTagName("c")[0].childNodes[0].nodeValue, item.getElementsByTagName("v")[0].childNodes[0].nodeValue, item.getElementsByTagName("t")[0].childNodes[0].nodeValue, mediacomputer);
            }
            document.getElementById("UPCOMING").innerHTML = data;
        }
    },
    function (a) {
        playlistValue = "";
    });
}
function buildPlaylistItem(cid, vitemid, title, mediacomputer)
{
    return "<div class=\"PITEM\">" +
    "<a onclick=\"return controlsRemoveItem('" + mediacomputer + "', '" + cid + "')\" href=\"" + getBaseURL() + "/ubermedia/control?mc=" + mediacomputer + "&amp;cmd=remove&amp;manual=1&amp;cid=" + cid + "\" class=\"x\">X</a>" +
    "<a href=\"" + getBaseURL() + "/ubermedia/item/" + vitemid + "\" class=\"i\"><img src=\"" + getBaseURL() + "/ubermedia/thumbnail/" + vitemid + "\" alt=\"" + title + "\'s thumbnail\" title=\"" + title + "\'s thumbnail\" /></a><a onclick=\"return controlsPlayItem('" + mediacomputer + "', '" + cid + "')\" href=\"" + getBaseURL() + "/ubermedia/control?mc=" + mediacomputer + "&amp;cmd=play_now&amp;manual=1&amp;cid=" + cid + "\" class=\"t\">" + title + "</a>" +
    "</div>";
}
function controlsButton(mediacomputer, cmd)
{
    Ajax(getBaseURL() + "/ubermedia/control?mc=" + encodeURI(mediacomputer) + "&cmd=" + cmd, "GET",
    function () { }, function () { });
    return false;
}
function controlsRemoveItem(mediacomputer, cid)
{
    Ajax(getBaseURL() + "/ubermedia/control?mc=" + encodeURI(mediacomputer) + "&cmd=remove&cid=" + encodeURI(cid), "GET",
    function () { }, function () { alert("Could not delete item, check you have a connection to the website!"); });
    return false;
}
function controlsPlayItem(mediacomputer, cid)
{
    Ajax(getBaseURL() + "/ubermedia/control?mc=" + encodeURI(mediacomputer) + "&cmd=play_now&cid=" + encodeURI(cid), "GET",
    function () { }, function () { alert("Could not play item, check you have a connection to the website!"); });
    return false;
}
function timeString(seconds)
{
    var hours = Math.floor(seconds / 3600);
    var minutes = Math.floor(seconds / 60) - (hours * 60);
    var seconds = seconds - (hours * 3600) - (minutes * 60);
    return (hours.toString().length == 1 ? "0" + hours : hours) + ":" + (minutes.toString().length == 1 ? "0" + minutes : minutes) + ":" + (seconds.toString().length == 1 ? "0" + seconds : seconds);
}
function itemPlay(vitemid)
{
    Ajax(getBaseURL() + "/ubermedia/item/" + vitemid + "/play_now", "GET",
    function()
    {
    },
    function()
    {
        alert("Could not play item, check you are connected to the site and the item still exists!");
    });
    return false;
}
function itemQueue(vitemid)
{
    Ajax(getBaseURL() + "/ubermedia/item/" + vitemid + "/add_to_queue", "GET",
    function()
    {
    },
    function()
    {
        alert("Could not queue item, check you are connected to the site and the item still exists!");
    });
    return false;
}
function adminStatus()
{
    setInterval(adminGetStatus, 1000);
}
function adminGetStatus()
{
    Ajax(getBaseURL() + "/ubermedia/admin?ajax=1", "get",
    function (a)
    {
        var doc = a.responseXML.getElementsByTagName("admin")[0];
        document.getElementById("service_indexing").innerHTML = doc.getElementsByTagName("indexing")[0].childNodes[0].nodeValue;
        document.getElementById("service_thumbnail").innerHTML = doc.getElementsByTagName("thumbnails")[0].childNodes[0].nodeValue;
        document.getElementById("service_filminformation").innerHTML = doc.getElementsByTagName("filminformation")[0].childNodes[0].nodeValue;
        document.getElementById("service_conversion").innerHTML = doc.getElementsByTagName("conversion")[0].childNodes[0].nodeValue;
    },
    function (a)
    {
        document.getElementById("service_indexing").innerHTML = "Client-side error: failed to retrieve dynamic update...";
        document.getElementById("service_thumbnail").innerHTML = "Client-side error: failed to retrieve dynamic update...";
        document.getElementById("service_filminformation").innerHTML = "Client-side error: failed to retrieve dynamic update...";
        document.getElementById("service_conversion").innerHTML = "Client-side error: failed to retrieve dynamic update...";
    }
    );
}