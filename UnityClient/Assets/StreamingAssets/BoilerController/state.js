var page = undefined;
    var pageStack = new Array();
    var tag = undefined;
    var controlState = {
        oil1: { control: 'Stop', state: "On" },
        oil2: { control: 'Stop', state: "On" },
        feed1: { control: 'Stop', state: "Off" },
        feed2: { control: 'Stop', state: "Off" },
        circ1: { control: 'Stop', state: "Off" },
        circ2: { control: 'Stop', state: "Off" }
    };
    function SwitchPage(target) {
        if (page != target) {
            let prev = document.getElementById(page);
            if (prev != undefined)
                prev.style.display = "none";
            let next = document.getElementById(target);

            next.style.display = "block";
            //hide back button on main page
            let backelement = document.getElementById("backbutton");
            backelement.style.display = (target === "main") ? "none" : "block";
            page = target;
        }
        StateRefresh();
    }
    function Navigate(target, newtag) {
        if (page != target) {
            let prevobject =
                {
                    p: page,
                    t: tag
                };
            tag = newtag;

            pageStack.push(prevobject);
            SwitchPage(target);
        }
    }
    function NavigateBack() {
        if (pageStack.length > 0) {
            let prev = pageStack.pop();
            tag = prev.t;
            SwitchPage(prev.p);
        }
    }
    function ParseAndUpdate(responce) {
	    let data = JSON.parse(responce);
        controlState=data;
        StateRefresh();
    }
    var pageUrl="BoilerControl.json";
    function request_update(id, command) {
        var params = JSON.stringify({"id": id, "command": command});
        var xhr = new XMLHttpRequest();
        xhr.open('POST', pageUrl, true);
		xhr.setRequestHeader("Content-Type", "application/json");
        xhr.send(params);
        xhr.onreadystatechange = function () {
            if (xhr.readyState == 4 && xhr.status == 200) {
                cParseAndUpdate(updateRequest.responseText);
            }
        }
    }
    var updateRequest;
    function request_periodical_update() {
        if (!updateRequest) {
            updateRequest = new XMLHttpRequest();
            updateRequest.onreadystatechange = function () {
                if (updateRequest.readyState == 4) {
                    if (updateRequest.status == 200) {
                        ParseAndUpdate(updateRequest.responseText);
                    }
                }
            }
        }
        if (updateRequest.readyState == 4 || updateRequest.readyState == 0) {
            updateRequest.open('GET', pageUrl, true);
            updateRequest.send(null);
        }
    }
    function StateRefresh() {
        let tag = window.tag;
        let pageref = document.getElementById(page);
        switch (page) {
            case "pumpControl": {
                let inputs = pageref.getElementsByTagName('a');
                let control = controlState[tag].control;
                for (item of inputs) {
                    let classname = (item.id == control) ? "togglebuttonset" : "togglebutton"
                    item.setAttribute("class", classname);
                }
            } break;
            case "oilpumpState":
            case "feedpumpState":
            case "circpumpState": {
                let inputs = pageref.getElementsByTagName('*');
                for (item of inputs) {
                    let dataname = item.getAttribute('data-name');
                    let datahint = item.getAttribute('data-hint');
                    if (datahint != undefined && dataname != undefined) {
                        let pumpstate = controlState[dataname];
                        let value = pumpstate[datahint];
                        item.innerHTML = value;
                    }
                }
            }
                break;
        }
    }
    function SwitchPump(to) {
        let tag = window.tag;
        controlState[tag].control = to;
        request_update(tag,to);
        StateRefresh();
    }