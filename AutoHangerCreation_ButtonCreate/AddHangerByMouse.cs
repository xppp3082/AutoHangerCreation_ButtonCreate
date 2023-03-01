#region Namespaces
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;
using System.Linq;
using System;

#endregion
namespace AutoHangerCreation_ButtonCreate
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class AddHangerByMouse : IExternalCommand
    {
#if RELEASE2019
        public static DisplayUnitType unitType = DisplayUnitType.DUT_CENTIMETERS;
#else
        public static ForgeTypeId unitType = UnitTypeId.Centimeters;
#endif
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            while (true)
            {
                try
                {
                    UIDocument uidoc = commandData.Application.ActiveUIDocument;
                    Document doc = uidoc.Document;
                    Autodesk.Revit.UI.Selection.ISelectionFilter pipeFilter = new PipeSelectionFilter(doc);
                    XYZ previousPt = null;
                    Reference previousRefer = null;
                    ReferenceArray reArray = null;
                    PreviewControl pControl = new PreviewControl(doc, doc.ActiveView.Id);
                    //pControl.MouseMove += MessageBox.Show("");

                    Reference refer = uidoc.Selection.PickObject(ObjectType.PointOnElement, pipeFilter, "請點選欲放置管架的位置");
                    XYZ position = refer.GlobalPoint;

                    //取得管的locationCurve後進行投影，取得垂直於管上的點位
                    Element elem = doc.GetElement(refer.ElementId);
                    LocationCurve pipeCrv = elem.Location as LocationCurve;
                    Curve curve = pipeCrv.Curve;
                    Line pipeLine = curve as Line;
                    IntersectionResult intersect = curve.Project(position);
                    XYZ targetPoint = intersect.XYZPoint;

                    //根據選中的管徑進行元件類型的篩選
                    Parameter targetPara = null;
                    switch (elem.Category.Name)
                    {
                        case "管":
                            targetPara = elem.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                            break;
                        case "電管":
                            targetPara = elem.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
                            break;
                        case "風管":
                            targetPara = elem.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                            break;
                    }
                    if (targetPara == null)
                    {
                        MessageBox.Show("目前還暫不適用方形管件，請待後續更新");
                    }
                    using (Transaction trans = new Transaction(doc))
                    {
                        trans.Start("放置單管吊架");
                        var degrees = 0.0;
                        double half_PI = Math.PI / 2;
                        double pipeDia = targetPara.AsDouble();
                        XYZ target_up = new XYZ(targetPoint.X, targetPoint.Y, targetPoint.Z + 1);
                        XYZ rotateBase = new XYZ(0, targetPoint.X, 0);
                        Line Axis = Line.CreateBound(targetPoint, target_up);
                        FamilySymbol targetSymbol = new pipeHanger().getFamilySymbol(doc, pipeDia);
                        Element hanger = new pipeHanger().CreateHanger(uidoc.Document, targetPoint, elem, targetSymbol);
                        degrees = rotateBase.AngleTo(pipeLine.Direction);
                        double a = degrees * 180 / (Math.PI);
                        double finalRotate = Math.Abs(half_PI - degrees);
                        if (a > 135 || a < 45)
                        {
                            finalRotate = -finalRotate;
                        }
                        //旋轉後校正位置
                        hanger.Location.Rotate(Axis, finalRotate);
                        previousPt = targetPoint;
                        previousRefer = new Reference(hanger);
                        trans.Commit();
                    }
                }
                catch
                {
                    break;
                }
            }
            //XYZ position = uidoc.Selection.PickPoint(ObjectSnapTypes.Points, "請點選欲放置管架的位置");
            //MessageBox.Show($"點選的物件名稱為{elem.Name}，選中點的X值為:{position.X}，Y值為{position.Y}，Z值為{position.Z}");
            Counter.count += 1;
            return Result.Succeeded;
        }

    }
}
