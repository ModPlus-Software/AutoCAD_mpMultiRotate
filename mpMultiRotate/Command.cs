namespace mpMultiRotate;

using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using ModPlusAPI;
using ModPlusAPI.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

/// <summary>
/// Main command
/// </summary>
public class Command
{
    /// <summary>
    /// Start
    /// </summary>
    [CommandMethod("ModPlus", "mpMultiRotate", CommandFlags.UsePickSet)]
    public void Start()
    {
#if !DEBUG
        Statistic.SendCommandStarting(new ModPlusConnector());
#endif
        const string langItem = "mpMultiScale";
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        var db = doc.Database;
        var ed = doc.Editor;

        // Переменная "Копия"
        var isCopy = false;

        try
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var opts = new PromptSelectionOptions();

                // Копия
                opts.Keywords.Add(Language.GetItem(langItem, "msg1"));
                var kws = opts.Keywords.GetDisplayString(true);

                // Выберите объекты:
                opts.MessageForAdding = $"\n{Language.GetItem(langItem, "msg2")}{kws}";

                // Implement a callback for when keywords are entered
                opts.KeywordInput += (_, e) =>
                {
                    if (e.Input.Equals(Language.GetItem(langItem, "msg1")))
                        isCopy = !isCopy;
                };

                var res = ed.GetSelection(opts);
                if (res.Status != PromptStatus.OK)
                    return;
                var selSet = res.Value;
                var idArr = selSet.GetObjectIds();
                if (idArr == null)
                    return;
                var idArray = isCopy ? SetCopy(idArr) : idArr;

                // Угол поворота в градусах:
                var integerOpt = new PromptIntegerOptions($"\n{Language.GetItem("msg3")}")
                {
                    AllowNegative = false,
                    AllowNone = false,
                    AllowZero = false,
                    LowerLimit = 1,
                    UpperLimit = 359
                };

                var integerRes = ed.GetInteger(integerOpt);
                if (integerRes.Status != PromptStatus.OK)
                    return;

                var pdOpt = new PromptKeywordOptions(string.Empty);
                var sVal = Language.GetItem(langItem, "kw1"); // Начальное значение
                pdOpt.AllowArbitraryInput = true;
                pdOpt.AllowNone = true;

                // pdOpt.SetMessageAndKeywords(
                //    "\n" + "Выберите базовую точку: " + "<" + sVal +
                //    ">: " + "[Центр/ЛНиз/ЛВерх/ПНиз/ПВерх/СНиз/СВерх/СЛево/СПраво]",
                //    "Центр ЛНиз ЛВерх ПНиз ПВерх СНиз СВерх СЛево СПраво");

                pdOpt.SetMessageAndKeywords(
                    $"\n{Language.GetItem(langItem, "msg4")}<{sVal}>: [{Language.GetItem(langItem, "kw1")}/{Language.GetItem(langItem, "kw2")}/{Language.GetItem(langItem, "kw3")}/{Language.GetItem(langItem, "kw5")}/{Language.GetItem(langItem, "kw4")}/{Language.GetItem(langItem, "kw6")}/{Language.GetItem(langItem, "kw7")}/{Language.GetItem(langItem, "kw8")}/{Language.GetItem(langItem, "kw9")}]",
                    $"{Language.GetItem(langItem, "kw1")} {Language.GetItem(langItem, "kw2")} {Language.GetItem(langItem, "kw3")} {Language.GetItem(langItem, "kw5")} {Language.GetItem(langItem, "kw4")} {Language.GetItem(langItem, "kw6")} {Language.GetItem(langItem, "kw7")} {Language.GetItem(langItem, "kw8")} {Language.GetItem(langItem, "kw9")}");
                var promptResult = ed.GetKeywords(pdOpt);
                if (promptResult.Status != PromptStatus.OK)
                    return;
                sVal = promptResult.StringResult;
                foreach (var objId in idArray)
                {
                    var ent = (Entity)tr.GetObject(objId, OpenMode.ForWrite);
                    var extPts = ent.GeometricExtents;
                    var pt1 = extPts.MinPoint;
                    var pt3 = extPts.MaxPoint;
                    var pt = default(Point3d);
                    if (sVal.Equals(Language.GetItem(langItem, "kw1")))
                        pt = new Point3d((pt1.X + pt3.X) / 2, (pt1.Y + pt3.Y) / 2, (pt1.Z + pt3.Z) / 2);
                    else if (sVal.Equals(Language.GetItem(langItem, "kw2")))
                        pt = pt1;
                    else if (sVal.Equals(Language.GetItem(langItem, "kw3")))
                        pt = new Point3d(pt1.X, pt3.Y, 0.0);
                    else if (sVal.Equals(Language.GetItem(langItem, "kw4")))
                        pt = pt3;
                    else if (sVal.Equals(Language.GetItem(langItem, "kw5")))
                        pt = new Point3d(pt3.X, pt1.Y, 0.0);
                    else if (sVal.Equals(Language.GetItem(langItem, "kw6")))
                        pt = new Point3d((pt1.X + pt3.X) / 2, pt1.Y, 0.0);
                    else if (sVal.Equals(Language.GetItem(langItem, "kw7")))
                        pt = new Point3d((pt1.X + pt3.X) / 2, pt3.Y, 0.0);
                    else if (sVal.Equals(Language.GetItem(langItem, "kw8")))
                        pt = new Point3d(pt1.X, (pt1.Y + pt3.Y) / 2, 0.0);
                    else if (sVal.Equals(Language.GetItem(langItem, "kw9")))
                        pt = new Point3d(pt3.X, (pt1.Y + pt3.Y) / 2, 0.0);
                    
                    var mat = Matrix3d.Rotation(ToRadians(integerRes.Value), Vector3d.ZAxis, pt);
                    ent.TransformBy(mat);
                }

                tr.Commit();
            }
        }
        catch (System.Exception exception)
        {
            exception.ShowInExceptionBox();
        }
    }

    private static ObjectId[] SetCopy(IEnumerable<ObjectId> idArray)
    {
        var list = new List<ObjectId>();
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        var db = doc.Database;

        try
        {
            // Используем транзакцию
            var tr = db.TransactionManager.StartTransaction();
            using (tr)
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false);
                foreach (var objId in idArray)
                {
                    var ent = tr.GetObject(objId, OpenMode.ForWrite).Clone() as Entity;
                    btr.AppendEntity(ent);
                    tr.AddNewlyCreatedDBObject(ent, true);
                    if (ent != null)
                        list.Add(ent.ObjectId);
                }

                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            ExceptionBox.Show(ex);
        }

        return list.ToArray();
    }

    private double ToRadians(int degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}