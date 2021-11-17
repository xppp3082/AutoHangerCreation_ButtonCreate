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
    public class MultiHangerCreation : IExternalCommand
    {
        //創造多管吊架
        DisplayUnitType unitType = DisplayUnitType.DUT_MILLIMETERS;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Autodesk.Revit.UI.Selection.ISelectionFilter pipeFilter = new PipeSelectionFilter();

            //點選要一起放置多管吊架的管段
            IList<Reference> pickElements_Refer = uidoc.Selection.PickObjects(ObjectType.Element, pipeFilter, "請選擇欲放置吊架的管段");
            Document doc = uidoc.Document;
            IList<Element> pickElements = new List<Element>();

            //將pickElements_Refer得出來的reference型別轉換成element
            foreach (Reference refer in pickElements_Refer)
            {
                pickElements.Add(doc.GetElement(refer));
            }

            Transaction trans = new Transaction(doc);
            trans.Start("放置多管吊架");

            //set up from and ask for user information -->創造表格並詢問資訊
            Form2 form2 = new Form2(commandData);
            form2.ShowDialog();

            //grab string values from form1 and convert to respectives types
            //設定分割數值與各管管徑
            string divideValueString = form2.divideValue.ToString();
            string diameter1 = form2.pipeDiameter1.ToString();
            string diameter2 = form2.pipeDiameter2.ToString();
            string diameter3 = form2.pipeDiameter3.ToString();
            string diameter4 = form2.pipeDiameter4.ToString();
            string diameter5 = form2.pipeDiameter5.ToString();
            string diameter6 = form2.pipeDiameter6.ToString();
            string diameter7 = form2.pipeDiameter7.ToString();
            string diameter8 = form2.pipeDiameter8.ToString();

            //單位轉換
            double divideValue_doubleTemp = double.Parse(divideValueString);
            double divideValue_double = UnitUtils.ConvertToInternalUnits(divideValue_doubleTemp, unitType);



            foreach (Element element in pickElements)
            {
                //針對每個管去找familyType
                FamilySymbol multiHangerType = new MultiPipeHanger().FindhangerType(doc);

                //找到管的locationCurve
                LocationCurve locationCurve = element.Location as LocationCurve;
                Curve pipeCurve = locationCurve.Curve;

                XYZ pipeStart = pipeCurve.GetEndPoint(0);
                XYZ pipeEnd = pipeCurve.GetEndPoint(1);
                XYZ pipeEndAdjust = new XYZ(pipeEnd.X, pipeEnd.Y, pipeStart.Z);
                Line pipeLineProject = Line.CreateBound(pipeStart, pipeEndAdjust);

                double pipeLength = pipeCurve.Length;
                double param1 = pipeCurve.GetEndParameter(0);
                double param2 = pipeCurve.GetEndParameter(1);

                //計算叫分割的數量
                int step = (int)(pipeLength / divideValue_double);
                double paramCalc = param1 + ((param2 - param1)
                  * divideValue_double / pipeLength);

                //創造一個容器裝所有的點資料(位於線上的)
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
                    Element hanger = new MultiPipeHanger().CreateMultiHanger(uidoc.Document, p1, element, multiHangerType);
                    XYZ p2 = new XYZ(p1.X, p1.Y, p1.Z + 1);
                    Line Axis = Line.CreateBound(p1, p2);
                    XYZ p3 = new XYZ(0, p1.X, 0); //測量吊架與管段之間的向量差異，取plane中的x向量

                    degrees = p3.AngleTo(pipeLineProject.Direction);
                    hanger.Location.Rotate(Axis, degrees);//旋轉吊架
                    hanger.LookupParameter("管直徑01").SetValueString(diameter1);
                    hanger.LookupParameter("管直徑02").SetValueString(diameter2);
                    hanger.LookupParameter("管直徑03").SetValueString(diameter3);
                    hanger.LookupParameter("管直徑04").SetValueString(diameter4);
                    hanger.LookupParameter("管直徑05").SetValueString(diameter5);
                    hanger.LookupParameter("管直徑06").SetValueString(diameter6);
                    hanger.LookupParameter("管直徑07").SetValueString(diameter7);
                    hanger.LookupParameter("管直徑08").SetValueString(diameter8);

                    //做最後的吊架對位修正
                    double transit = UnitUtils.ConvertToInternalUnits(double.Parse(diameter1), unitType); ;

                    XYZ translationVector = new XYZ(0, -transit/2 - (10 / 30.48), -transit/2);
                    ElementTransformUtils.MoveElement(doc, hanger.Id, translationVector);

                    //double offset = hanger.LookupParameter("偏移").AsDouble();
                }
                string total = pointList.Count.ToString();
                MessageBox.Show("共產生" + total + "個多管吊架");
            }
            trans.Commit();
            return Result.Succeeded;
        }


        class MultiPipeHanger
        {
            //創建多管class-->其中包含三項功能
            //1.找到family
            //2.蒐集各種管徑
            //3.寫入管徑參數
            //依點放置吊點+移動
            public FamilySymbol FindhangerType(Document doc)
            {
                //尋找吊架的族群(FamilySymbol)
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_PipeAccessory);
                IList<Element> all = collector.WhereElementIsElementType().ToElements();
                FamilySymbol targetFamily = null;

                foreach (Element e in all)
                {
                    FamilySymbol familySymbol = e as FamilySymbol;
                    try
                    {
                        if (familySymbol.Name == "多管吊架_管v8")
                        {
                            targetFamily = familySymbol;
                        }
                    }
                    catch (ArgumentNullException ex) when (targetFamily == null)
                    {
                        MessageBox.Show(ex.ToString());
                        MessageBox.Show("尚未匯入指定的多管吊架 !!!");
                    }
                }
                return targetFamily;
            }
            public FamilyInstance CreateMultiHanger(Document doc, XYZ location, Element element, FamilySymbol targetFamily)
            {
                FamilyInstance instance = null;
                if (targetFamily != null)
                {
                    targetFamily.Activate();
                    MEPCurve pipCrv = element as MEPCurve;
                    Level HangLevel = pipCrv.ReferenceLevel;

                    double movedown = HangLevel.Elevation;//取得該層樓高層
                    instance = doc.Create.NewFamilyInstance(location, targetFamily, HangLevel, StructuralType.NonStructural);
                    double toMove = (instance.LookupParameter("偏移").AsDouble()) - movedown;
                    instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM).Set(toMove);
                }
                return instance;
            }
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

