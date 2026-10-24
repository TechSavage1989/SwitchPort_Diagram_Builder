namespace HUBTechGenerator;

// CIEDE2000 with unit parametric factors, using CIELAB D65 coordinates.
// Formula and validation data: Sharma, Wu and Dalal, Color Research and
// Application 30(1), 2005, DOI 10.1002/col.20070.
// https://hajim.rochester.edu/ece/sites/gsharma/ciede2000/
public static class PerceptualColor
{
    public static double Difference(string first,string second)=>Difference(AggregatePalette.Lab(first),AggregatePalette.Lab(second));
    public static double Difference((double L,double A,double B) first,(double L,double A,double B) second)
    {
        const double k25=6103515625.0;
        double Rad(double degrees)=>degrees*Math.PI/180;
        double Degrees(double a,double b)
        {
            if(a==0&&b==0)return 0;
            double h=Math.Atan2(b,a)*180/Math.PI;return h<0?h+360:h;
        }
        double c1=Math.Sqrt(first.A*first.A+first.B*first.B),c2=Math.Sqrt(second.A*second.A+second.B*second.B);
        double meanC=(c1+c2)/2,c7=Math.Pow(meanC,7),g=.5*(1-Math.Sqrt(c7/(c7+k25)));
        double a1=(1+g)*first.A,a2=(1+g)*second.A;
        double cp1=Math.Sqrt(a1*a1+first.B*first.B),cp2=Math.Sqrt(a2*a2+second.B*second.B);
        double h1=Degrees(a1,first.B),h2=Degrees(a2,second.B),dh=h2-h1;
        if(cp1*cp2==0)dh=0;else if(dh>180)dh-=360;else if(dh< -180)dh+=360;
        double deltaL=second.L-first.L,deltaC=cp2-cp1,deltaH=2*Math.Sqrt(cp1*cp2)*Math.Sin(Rad(dh/2));
        double l=(first.L+second.L)/2,c=(cp1+cp2)/2,h;
        if(cp1*cp2==0)h=h1+h2;
        else if(Math.Abs(h1-h2)<=180)h=(h1+h2)/2;
        else h=(h1+h2+(h1+h2<360?360:-360))/2;
        double t=1-.17*Math.Cos(Rad(h-30))+.24*Math.Cos(Rad(2*h))+.32*Math.Cos(Rad(3*h+6))-.20*Math.Cos(Rad(4*h-63));
        double sl=1+.015*(l-50)*(l-50)/Math.Sqrt(20+(l-50)*(l-50)),sc=1+.045*c,sh=1+.015*c*t;
        double theta=30*Math.Exp(-Math.Pow((h-275)/25,2)),cp7=Math.Pow(c,7),rt=-2*Math.Sqrt(cp7/(cp7+k25))*Math.Sin(Rad(2*theta));
        double dl=deltaL/sl,dc=deltaC/sc,dH=deltaH/sh;
        return Math.Sqrt(Math.Max(0,dl*dl+dc*dc+dH*dH+rt*dc*dH));
    }
}
