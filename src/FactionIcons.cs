using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace Genevo {
static class FactionIcons {
    public static string Kind(string faction){string s=(faction??"").ToLowerInvariant();if(s.Contains("미국")||s.Contains("america")||s.Contains("usa"))return "USA";if(s.Contains("중국")||s.Contains("china"))return "CHINA";if(s.Contains("gla"))return "GLA";return "UNKNOWN";}
    public static void Draw(Graphics g,Rectangle box,string faction){var state=g.Save();g.TranslateTransform(box.X,box.Y);g.ScaleTransform(box.Width/32f,box.Height/32f);g.SmoothingMode=SmoothingMode.AntiAlias;string kind=Kind(faction);Color color=kind=="USA"?Color.FromArgb(105,182,235):kind=="CHINA"?Color.FromArgb(238,188,67):kind=="GLA"?Color.FromArgb(104,197,118):Color.Gray;
        using(var background=new SolidBrush(Color.FromArgb(20,36,48)))g.FillEllipse(background,1,1,30,30);using(var pen=new Pen(color,1.5f))g.DrawEllipse(pen,1,1,30,30);
        using(var fill=new SolidBrush(color))using(var line=new Pen(color,2f)){
            if(kind=="USA"){g.FillPolygon(fill,new[]{new Point(4,12),new Point(13,15),new Point(16,8),new Point(19,15),new Point(28,12),new Point(24,20),new Point(19,19),new Point(16,26),new Point(13,19),new Point(8,20)});g.FillEllipse(fill,14,6,5,5);}
            else if(kind=="CHINA"){var star=new PointF[10];for(int n=0;n<10;n++){double a=-Math.PI/2+n*Math.PI/5;float r=n%2==0?11:4.5f;star[n]=new PointF(16+(float)Math.Cos(a)*r,16+(float)Math.Sin(a)*r);}g.FillPolygon(fill,star);}
            else if(kind=="GLA"){g.FillEllipse(fill,12,14,8,12);g.DrawBezier(line,17,24,29,20,27,5,18,7);g.FillPolygon(fill,new[]{new Point(18,4),new Point(17,10),new Point(22,8)});g.DrawLine(line,13,18,7,12);g.DrawLine(line,7,12,5,8);g.DrawLine(line,7,12,10,8);g.DrawLine(line,19,17,24,12);for(int n=0;n<3;n++){g.DrawLine(line,12,19+n*3,7,21+n*3);g.DrawLine(line,19,19+n*3,23,21+n*3);}}
            else using(var font=new Font("Segoe UI",16,FontStyle.Bold))g.DrawString("?",font,fill,8,3);
        }g.Restore(state);
    }
}
}
