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
    class PipeHangerSetUp:IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return Result.Succeeded;
            }
            catch
            {
                return Result.Failed;
            }
        }
    }
 //class HangerTypeCollection
 //   {
 //       private string seletion;
 //       //public List<string>getAllHangerNames(Document doc)
 //       //{

 //       //}
 //   }
}
