#region Namespaces
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Autodesk.Revit.DB.Structure;


#endregion

namespace AutoHangerCreation_ButtonCreate
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class HangerCreation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //UIDocument uidoc = commandData.Application.ActiveUIDocument;
            //ICollection<ElementId> ids = uidoc.Selection.GetElementIds();
            //Document doc = uidoc.Document; //以上為不分類所有東西的可以選的版本

            //限制使用者只能選中管
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Autodesk.Revit.UI.Selection.ISelectionFilter pipeFilter = new PipeSelectionFilter();


            IList<Element> pickElements = uidoc.Selection.PickElementsByRectangle(pipeFilter, "請選擇管");

            Document doc = uidoc.Document;
            Transaction trans = new Transaction(doc);
            trans.Start("放置管架");


            StringBuilder st = new StringBuilder();
            //st.AppendLine("位於以下座標的管已被選取:");


            foreach (Element element in pickElements)
            {
                FamilySymbol hangerType = findhangerType(element, doc); //選取要放置的吊件類型，前提是吊件已被匯入至此專案檔

                //找到管的location curve
                LocationCurve pipeLocationCrv = element.Location as LocationCurve;
                Curve pipeCurve = pipeLocationCrv.Curve;

                XYZ pipeStart = pipeCurve.GetEndPoint(0);
                XYZ pipeEnd = pipeCurve.GetEndPoint(1);

                XYZ pipeEndAdjust = new XYZ(pipeEnd.X, pipeEnd.Y, pipeStart.Z);
                Line pipelineProject = Line.CreateBound(pipeStart, pipeEndAdjust);

                double pipeLength = pipeCurve.Length;

                double requiredDist = 3; //需要安裝的距離(單位待確定)

                double param1 = pipeCurve.GetEndParameter(0);
                double param2 = pipeCurve.GetEndParameter(1);

                int step = (int)(pipeLength / requiredDist); //要分割的數量 (不確定是否有四捨五入)

                double paramCalc = param1 + ((param2 - param1)
                  * requiredDist / pipeLength);

                //創造一個容器裝所有點資料(位於線上的)
                IList<double> paramList = new List<double>();

                IList<Point> pointList = new List<Point>();
                IList<XYZ> locationList = new List<XYZ>();
                XYZ evaluatedPoint = null;
                var degrees = 0.0;

                for (int i = 0; i < step; i++)
                {
                    paramCalc = param1 + ((param2 - param1) * requiredDist * (i + 1) / pipeLength);
                    if (pipeCurve.IsInside(paramCalc) == true)
                    {
                        double normParam = pipeCurve.ComputeNormalizedParameter(paramCalc);

                        evaluatedPoint = pipeCurve.Evaluate(normParam, true);
                        Point locationPoint = Point.Create(evaluatedPoint);
                        //XYZ evaluatedProject = new XYZ(evaluatedPoint.X, evaluatedPoint.Y, 0);
                        pointList.Add(locationPoint);
                        locationList.Add(evaluatedPoint);
                    }

                }


                foreach (XYZ p1 in locationList)
                {
                    Element hanger = CreateHanger(uidoc.Document, p1, element, hangerType);
                    XYZ p2 = new XYZ(p1.X, p1.Y, p1.Z + 1);
                    Line Axis = Line.CreateBound(p1, p2);
                    XYZ p3 = new XYZ(p1.X, 0, 0); //測量吊架與管段之間的向量差異，取plane中的x向量

                    degrees = p3.AngleTo(pipelineProject.Direction);
                    //ElementTransformUtils.RotateElement(doc, hanger.Id, pipeLine, degrees); //旋轉吊架方法2
                    //ElementTransformUtils.RotateElement(doc, hanger.Id,Axis,degrees); //旋轉吊架方法2

                    hanger.Location.Rotate(Axis, degrees); //旋轉吊架方法1
                    double offset = hanger.LookupParameter("偏移").AsDouble();
                }

                string total = pointList.Count.ToString();
                MessageBox.Show("共產生" + total + "個吊架");

            }

            //MessageBox.Show(st.ToString());

            trans.Commit();
            return Result.Succeeded;

        }


        public FamilySymbol findhangerType(Element pipe, Document doc)
        {
            //尋找吊架的族群(FamilySymbol)
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_PipeAccessory);

            IList<Element> all = collector.WhereElementIsElementType().ToElements();

            FamilySymbol targetFamily = null;

            foreach (Element e in all)
            {
                FamilySymbol familySymbol = e as FamilySymbol;
                //if (familySymbol.Name == "DN100(4) x R1/2")
                //if(familySymbol.Name == "DN100(4\")x R1/2\"") //for 100mm
                //if (familySymbol.Name == "DN80(3\")x R1/2\"") //for 80mm
                //if (familySymbol.Name == "DN50(2\")x R3/8\"") //for 50mm
                //if (familySymbol.Name == "DN32(1¼\")x R3/8\"") //for 25mm

                if (pipe.LookupParameter("大小").AsString() == "100 mmø")
                {
                    if (familySymbol.Name == "DN100(4\")x R1/2\"")
                    {
                        targetFamily = familySymbol;
                    }
                }
                else if (pipe.LookupParameter("大小").AsString() == "80 mmø")
                {
                    if (familySymbol.Name == "DN80(3\")x R1/2\"")
                    {
                        targetFamily = familySymbol;
                    }
                }
                else if (pipe.LookupParameter("大小").AsString() == "50 mmø")
                {
                    if (familySymbol.Name == "DN50(2\")x R3/8\"")
                    {
                        targetFamily = familySymbol;
                    }
                }
                else if (pipe.LookupParameter("大小").AsString() == "25 mmø")
                {
                    if (familySymbol.Name == "DN32(1¼\")x R3/8\"")
                    {
                        targetFamily = familySymbol;
                    }
                }
                else
                {
                    if (familySymbol.Name == "DN100(4\")x R1/2\"")
                    {
                        targetFamily = familySymbol;
                    }
                }
            }
            return targetFamily;
        }

        public FamilyInstance CreateHanger(Document doc, XYZ location, Element element, FamilySymbol targetFamily)
        {
            //創造吊架
            FamilyInstance instance = null;
            if (targetFamily != null)
            {
                targetFamily.Activate();

                if (null != targetFamily)
                {
                    //Level HangLevel = doc.GetElement(element.LevelId) as Level; //取得管件的參考樓層，但不知道為什麼不可行
                    MEPCurve pipCrv = element as MEPCurve; //選取管件，一定可以轉型MEPCurve
                    Level HangLevel = pipCrv.ReferenceLevel;

                    double moveDown = HangLevel.Elevation; //取得該層樓高層


                    instance = doc.Create.NewFamilyInstance(location, targetFamily, HangLevel, StructuralType.NonStructural); //一定要宣告structural 類型? yes
                    double toMove = (instance.LookupParameter("偏移").AsDouble()) - moveDown;
                    instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM).Set(toMove); //因為給予instance reference level後，實體會基於level的高度上進行偏移，因此需要將偏移量再扣掉一次，非常重要 !!!!。
                }
            }

            return instance;

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
    }
}
