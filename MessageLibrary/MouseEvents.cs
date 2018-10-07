using System;


namespace MessageLibrary
{
    public enum MouseEventType
    {
        Done = -1,
        ButtonDown = 0,
        ButtonUp = 1,
        Move = 2,
        Leave=3,
        Wheel=4,
       

    }

    public enum MouseButton
    {
        Left=0,
        Right=1,
        Middle=2,
        None=4
    }

    [Serializable]
    public class MouseMessage:AbstractEvent
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Delta { get; set; }
        public MouseEventType Type { get; set; }
        public MouseButton Button { get; set; }

        /*protected override bool Compare(AbstractEvent ev2)
        {
            MouseMessage ge = ev2 as MouseMessage;

            return (X == ge.X && Y == ge.Y&&Delta==ge.Delta&&Type==ge.Type&&Button==ge.Button);
        }*/

    }
}
