using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using SimpleWebBrowser;
using UnityEngine;


[Serializable]
class PumpState {
    public string control="Stop";
    public string state="Off";
}
[Serializable]
class PumpCommand {
    public string id;
    public string command;
}
[Serializable]
class PumpSettings {
    public PumpState oil1=new PumpState();
    public PumpState oil2=new PumpState();
    public PumpState feed1=new PumpState();
    public PumpState feed2=new PumpState();
    public PumpState circ1=new PumpState();
    public PumpState circ2=new PumpState();
}

[Serializable]
public class SampleDynamicHandler : MonoBehaviour,IDynamicRequestHandler {
    private readonly PumpSettings _settings = new PumpSettings();
    private string _previousstate;

    PumpState Search(string name) {
        var fueld = _settings.GetType().GetField(name);
        if (fueld == null)
            return null;
        return (PumpState)fueld.GetValue(_settings);
    }
    public string Request(string url, string query) {
        if (query != null) {
            var command = JsonUtility.FromJson<PumpCommand>(query);
            if (command != null) {
                var target = Search(command.id);
                if (target != null)
                    target.control = command.command;
            }
        }
        if (query != null || _previousstate == null) {
            _previousstate = JsonUtility.ToJson(_settings);
        } 
        return _previousstate;
    }
}
