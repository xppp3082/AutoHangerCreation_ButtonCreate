﻿using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Autodesk.Revit.DB.Structure;
using System;
using Autodesk.Revit.UI.Selection;
using System.Linq;

namespace AutoHangerCreation_ButtonCreate
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class HangerToFloorDist : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Autodesk.Revit.UI.Selection.ISelectionFilter pipeAccess_Filter = new PipeAccessoryFilter();

            //點選要更改牙桿長度的吊架
            List<Element> pickAccessory = new List<Element>();
            Document doc = uidoc.Document;
            IList<Reference> pickAccessory_Refer = uidoc.Selection.PickObjects(ObjectType.Element, pipeAccess_Filter, $"請選整欲調整牙桿長度的吊架");

            foreach (Reference reference in pickAccessory_Refer)
            {
                Element element = doc.GetElement(reference.ElementId);
                pickAccessory.Add(element);
            }

            //ICollection<ElementId> ids = uidoc.Selection.GetElementIds();
            //ICollection<Element> elementList = new List<Element>();

            Transaction trans = new Transaction(doc);
            trans.Start("調整螺牙長度");
            //將選到的物件寫進iList中
            foreach (Element elem in pickAccessory)
            {
                FamilyInstance instance = elem as FamilyInstance;
                double threadLength = CalculateDist_upperLevel(doc, instance);
                instance.LookupParameter("管到樓板距離").Set(threadLength);
            }

            trans.Commit();
            MessageBox.Show("螺桿長度調整完畢!");
            return Result.Succeeded;
        }

        private double CalculateDist_upperLevel(Document doc, FamilyInstance hanger)
        {
            //利用ReferenceIntersector回傳吊架location point 和上層樓板之間的距離

            //Find a 3D view to use for ReferenceIntersector constructor
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            Func<View3D, bool> isNotTemplate = v3 => !(v3.IsTemplate);
            View3D view3D = collector.OfClass(typeof(View3D)).Cast<View3D>().First<View3D>(isNotTemplate);

            //Find the locationiPoint of Hanger as the start point
            LocationPoint hangerLocation = hanger.Location as LocationPoint;
            XYZ startLocation = hangerLocation.Point;

            //Project in the positive Z direction on to the floor
            XYZ rayDirectioin = new XYZ(0, 0, 1);

            ElementClassFilter filter = new ElementClassFilter(typeof(Floor));

            ReferenceIntersector referenceIntersector = new ReferenceIntersector(filter, FindReferenceTarget.Face, view3D);

            //FindReferencesInRevitLinks=true 打開對於外參的測量
            referenceIntersector.FindReferencesInRevitLinks = true;
            ReferenceWithContext referenceWithContext = referenceIntersector.FindNearest(startLocation, rayDirectioin);

            Reference reference = referenceWithContext.GetReference();
            XYZ intersection = reference.GlobalPoint;

            double dist = startLocation.DistanceTo(intersection);

            return dist;
        }

        public class PipeAccessoryFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element.Category.Name == "管附件")
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