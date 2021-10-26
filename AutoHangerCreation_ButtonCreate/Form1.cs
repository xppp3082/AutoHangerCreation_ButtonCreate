using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AutoHangerCreation_ButtonCreate
{

    public partial class Form1 : System.Windows.Forms.Form
    {
        private UIApplication uiapp;
        private UIDocument uidoc;
        private Autodesk.Revit.ApplicationServices.Application app;
        private Document doc;

        public string divideValue;


        public Form1(ExternalCommandData commandData)
        {
            InitializeComponent();

            uiapp = commandData.Application;
            uidoc = uiapp.ActiveUIDocument;
            app = uiapp.Application;
            doc = uidoc.Document;
        }

        private void divideLength_TextChanged(object sender, EventArgs e)
        {

        }

        private void continueButton_Click(object sender, EventArgs e)
        {
            divideValue = divideLengthTextBox.Text;

            continueButton.DialogResult = DialogResult.OK;
            Debug.WriteLine("Ok button was clicked.");
            Close();

            return;
        }
        private void cancelButton_Click(object sender, EventArgs e)
        {
            cancelButton.DialogResult = DialogResult.Cancel;
            Debug.WriteLine("Cancel button was clicked"); //呼叫debug必須要引用using System.Diagnostics;
        }
    }
}
