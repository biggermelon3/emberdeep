using UnityEngine;

namespace FireRescue
{
    public enum FireEquipmentId { Extinguisher, PulsePistol, RainFan, PressureCannon }
    public enum FireDelivery { Foam, Pulse, Fan, Charge }
    public sealed class FireEquipment
    {
        public readonly FireEquipmentId Id;
        public readonly string Name, Description, Controls;
        public readonly FireDelivery Delivery;
        public readonly int Price, Unlock;
        public readonly float Capacity, Power, Range, Speed, Width, Flow;
        public readonly Color Color;
        FireEquipment(FireEquipmentId id,string name,string description,string controls,FireDelivery delivery,int price,int unlock,float capacity,float power,float range,float speed,float width,float flow,Color color)
        {Id=id;Name=name;Description=description;Controls=controls;Delivery=delivery;Price=price;Unlock=unlock;Capacity=capacity;Power=power;Range=range;Speed=speed;Width=width;Flow=flow;Color=color;}
        public static readonly FireEquipment[] All={
            new FireEquipment(FireEquipmentId.Extinguisher,"FOAM EXTINGUISHER","Short sweep / foam slows fire spread","HOLD pinch: foam  |  DOCK to refill",FireDelivery.Foam,0,0,60,45,14,2.8f,.22f,14,new Color(.85f,.08f,.045f)),
            new FireEquipment(FireEquipmentId.PulsePistol,"PULSE WATER PISTOL","Accurate bursts / longer reach","TAP: burst  |  HOLD: repeat bursts",FireDelivery.Pulse,100,1,75,85,23,4.1f,.18f,14,new Color(.03f,.58f,.85f)),
            new FireEquipment(FireEquipmentId.RainFan,"RAIN FAN","Five streams / wide cover / thirsty","HOLD: sweep a row  |  DOCK to refill",FireDelivery.Fan,175,2,110,110,19,3.2f,.4f,24,new Color(.09f,.7f,.34f)),
            new FireEquipment(FireEquipmentId.PressureCannon,"PRESSURE CANNON","Charge a water bomb / area splash","HOLD: charge  |  RELEASE: launch",FireDelivery.Charge,250,3,140,150,32,3.7f,1.4f,22,new Color(1,.46f,.035f))
        };
        public static FireEquipment Get(FireEquipmentId id)=>All[(int)id];
    }
}
