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


#endregion

namespace AutoHangerCreation_ButtonCreate
{
    //創造單管吊架
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class HangerCreation : IExternalCommand
    {
        DisplayUnitType unitType = DisplayUnitType.DUT_MILLIMETERS;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                //UIDocument uidoc = commandData.Application.ActiveUIDocument;
                //ICollection<ElementId> ids = uidoc.Selection.GetElementIds();
                //Document doc = uidoc.Document; 
                //以上為不分類所有東西的可以選的版本

                //限制使用者只能選中管
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Autodesk.Revit.UI.Selection.ISelectionFilter pipeFilter = new PipeSelectionFilter();

                //點選要一起放置單管吊架的管段
                List<Element> pickElements = new List<Element>();
                Document doc = uidoc.Document;
                IList<Reference> pickElements_Refer = uidoc.Selection.PickObjects(ObjectType.Element, pipeFilter, $"請選擇欲放置吊架的管段");

                foreach (Reference reference in pickElements_Refer)
                {
                    Element element = doc.GetElement(reference.ElementId);
                    pickElements.Add(element);
                }

                StringBuilder st = new StringBuilder();

                //set up form and ask for user information -->創造表格並詢問資訊
                PipeHangerUI UserForm = new PipeHangerUI(commandData);
                UserForm.ShowDialog();
                //grab string values from form1 and convert to respectives types
                string divideValueString = UserForm.divideValue.ToString();

                //英制轉公制
                double divideValue_doubleTemp = double.Parse(divideValueString);
                double divideValue_double = UnitUtils.ConvertToInternalUnits(divideValue_doubleTemp, unitType);
                int hangerCount = 0;



                Transaction trans = new Transaction(doc);
                trans.Start("放置單管吊架測試");

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

                    double param1 = pipeCurve.GetEndParameter(0);
                    double param2 = pipeCurve.GetEndParameter(1);

                    //計算要分割的數量 (不確定是否有四捨五入)
                    int step = (int)(pipeLength / divideValue_double);
                    double paramCalc = param1 + ((param2 - param1)
                      * divideValue_double / pipeLength);

                    //創造一個容器裝所有點資料(位於線上的)
                    IList<Point> pointList = new List<Point>();
                    IList<XYZ> locationList = new List<XYZ>();
                    XYZ evaluatedPoint = null;
                    var degrees = 0.0;

                    for (int i = 0; i < step; i++)
                    {
                        paramCalc = param1 + ((param2 - param1) * divideValue_double * (i + 1) / pipeLength);
                        if (pipeCurve.IsInside(paramCalc) == true)
                        {
                            double normParam = pipeCurve.ComputeNormalizedParameter(paramCalc);

                            evaluatedPoint = pipeCurve.Evaluate(normParam, true);
                            Point locationPoint = Point.Create(evaluatedPoint);
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

                        //旋轉後校正位置
                        hanger.Location.Rotate(Axis, degrees);
                        //double offset = hanger.LookupParameter("偏移").AsDouble();

                        //延伸吊架的牙桿長度
                        //FamilyInstance hangerinstance = hanger as FamilyInstance;
                        //double threadLength = CalculateDist_upperLevel(doc, hangerinstance);
                        //hangerinstance.LookupParameter("管到樓板距離").Set(threadLength);
                    }
                    string total = pointList.Count.ToString();
                    hangerCount += int.Parse(total);
                }

                MessageBox.Show("共產生" + hangerCount.ToString() + "個吊架");
                trans.Commit();
            }
            catch
            {
                return Result.Failed;
            }
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
