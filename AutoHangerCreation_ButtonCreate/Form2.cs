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
    public partial class Form2 : System.Windows.Forms.Form
    {
        private UIApplication uiapp;
        private UIDocument uidoc;
        private Autodesk.Revit.ApplicationServices.Application app;
        private Document doc;

        public string divideValue;
        public string pipeDiameter1, pipeDiameter2, pipeDiameter3,pipeDiameter4, pipeDiameter5, pipeDiameter6, pipeDiameter7, pipeDiameter8;



        public Form2(ExternalCommandData commandData)
        {
            InitializeComponent();
            uiapp = commandData.Application;
            uidoc = uiapp.ActiveUIDocument;
            app = uiapp.Application;
            doc = uidoc.Document;
        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }

        private void continueButton_Click(object sender, EventArgs e)
        {
            divideValue=divideLengthTextBox.Text;
            pipeDiameter1 = DiameterText01.Text;
            pipeDiameter2 = DiameterText02.Text;
            pipeDiameter3 = DiameterText03.Text;
            pipeDiameter4 = DiameterText04.Text;
            pipeDiameter5 = DiameterText05.Text;
            pipeDiameter6 = DiameterText06.Text;
            pipeDiameter7 = DiameterText07.Text;
            pipeDiameter8 = DiameterText08.Text;

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

        private void label2_Click(object sender, EventArgs e)
        {

        }
        private void label9_Click(object sender, EventArgs e)
        {

        }
    }
}
