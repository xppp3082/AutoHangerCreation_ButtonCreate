using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Autodesk.Revit.DB.Structure;
using System;
using Autodesk.Revit.UI.Selection;

namespace AutoHangerCreation_ButtonCreate
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class PipeDistTest : IExternalCommand
    {
        DisplayUnitType unitType = DisplayUnitType.DUT_MILLIMETERS;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Autodesk.Revit.UI.Selection.ISelectionFilter pipeFilter = new PipeSelectionFilter();

            //點選要一起放置多管吊架的管段
            //IList<Reference> pickElements_Refer = uidoc.Selection.PickObjects(ObjectType.Element, pipeFilter, "請選擇欲放置吊架的管段");
            IList<Element> pickElements = new List<Element>();
            Document doc = uidoc.Document;
            //管數量
            int pipeCount = 5;
            for (int i = 0; i < pipeCount; i++)
            {
                Reference pickElements_Refer = uidoc.Selection.PickObject(ObjectType.Element, pipeFilter, $"請選擇欲放置吊架的管段，還剩 {pipeCount-i} 隻管要選擇");
                Element element = doc.GetElement(pickElements_Refer.ElementId);
                pickElements.Add(element);
            }



            //將pickElements_Refer得出來的reference型別轉換成element
            //foreach (Reference refer in pickElements_Refer)
            //{
            //    pickElements.Add(doc.GetElement(refer));
            //}

            StringBuilder st = new StringBuilder();
            //創造一個容器，裝取依序點選的管徑
            List<double> pipeDiameters = new List<double>();

            //角度測量測試
            XYZ basePt = new XYZ(0, 1, 0);
            List<double> pipeAngle = new List<double>();


            foreach (Element pipe in pickElements)
            {
                double pipeDia = pipe.LookupParameter("直徑").AsDouble();
                LocationCurve locationCurve = pipe.Location as LocationCurve;
                Curve calCurve = locationCurve.Curve;
                XYZ calStr = calCurve.GetEndPoint(0);
                XYZ calEnd = calCurve.GetEndPoint(1);


                Line calCurveProject = Line.CreateBound(calStr, new XYZ(calEnd.X,calEnd.Y,calStr.Z));
                double angleTest = basePt.AngleTo(calCurveProject.Direction)*(180/Math.PI);
                pipeDiameters.Add(pipeDia);
                st.AppendLine(angleTest.ToString());
                //st.AppendLine(pipeDia.ToString());

            }
            MessageBox.Show(pipeDiameters.Count.ToString());
            MessageBox.Show("產生的夾角角度分別為:" + st.ToString());
            //MessageBox.Show("選中的管徑分別為:" + st.ToString());

            return Result.Succeeded;
        }
        public class PipeSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element.Category.Name == "管")
                {
                    return true;
                }
                return false;
            }
            public bool AllowReference(Reference refer, XYZ point)
            {
                return false;
            }
        }
        public XYZ sortPIpeByPt(Element element)
        {

            return new XYZ(0, 0, 0);
        }
    }
}
