using System.Collections.Generic;
using UnityEditor;

namespace XVR.Tools
{
    public enum VtoolLanguage
    {
        English = 0,
        Arabic = 1,
        Spanish = 2
    }

    public static class VtoolLocalization
    {
        private const string PrefsKey = "com.vtool.autofixer.language";

        private struct Entry
        {
            public string En;
            public string Ar;
            public string Es;
            public bool VrchatTermOnly;
        }

        private static readonly Dictionary<string, Entry> Table = new Dictionary<string, Entry>();
        private static bool ready;

        public static VtoolLanguage Language
        {
            get => (VtoolLanguage)EditorPrefs.GetInt(PrefsKey, (int)VtoolLanguage.English);
            set => EditorPrefs.SetInt(PrefsKey, (int)value);
        }

        public static string[] LanguageDisplayNames => new[]
        {
            "English",
            VtoolArabicImgui.Fix("العربية") + " (Arabic)",
            "Español (Spanish)"
        };

        public static string T(string key)
        {
            EnsureReady();
            if (!Table.TryGetValue(key, out var e))
                return key;

            if (e.VrchatTermOnly || Language == VtoolLanguage.English)
                return e.En;

            string local = Language == VtoolLanguage.Arabic ? e.Ar : e.Es;
            if (string.IsNullOrEmpty(local) || local == e.En)
                return e.En;

            if (Language == VtoolLanguage.Arabic)
                return VtoolArabicImgui.Fix(local) + " (" + e.En + ")";

            return local + " (" + e.En + ")";
        }

        public static string TF(string key, params object[] args)
        {
            EnsureReady();
            if (!Table.TryGetValue(key, out var e))
                return key;

            try
            {
                if (e.VrchatTermOnly || Language == VtoolLanguage.English)
                    return string.Format(e.En, args);

                string local = Language == VtoolLanguage.Arabic ? e.Ar : e.Es;
                if (string.IsNullOrEmpty(local) || local == e.En)
                    return string.Format(e.En, args);

                string formattedLocal = string.Format(local, args);
                string formattedEn = string.Format(e.En, args);

                if (Language == VtoolLanguage.Arabic)
                    return VtoolArabicImgui.Fix(formattedLocal) + " (" + formattedEn + ")";

                return formattedLocal + " (" + formattedEn + ")";
            }
            catch
            {
                return T(key);
            }
        }

        public static string Raw(string key)
        {
            EnsureReady();
            if (!Table.TryGetValue(key, out var e))
                return key;
            switch (Language)
            {
                case VtoolLanguage.Arabic:
                    return VtoolArabicImgui.Fix(string.IsNullOrEmpty(e.Ar) ? e.En : e.Ar);
                case VtoolLanguage.Spanish:
                    return string.IsNullOrEmpty(e.Es) ? e.En : e.Es;
                default:
                    return e.En;
            }
        }

        public static string EnglishOf(string key)
        {
            EnsureReady();
            return Table.TryGetValue(key, out var e) ? e.En : key;
        }

        private static void EnsureReady()
        {
            if (ready) return;
            ready = true;
            RegisterAll();
        }

        private static void Add(string key, string en, string ar, string es, bool vrchatTermOnly = false)
        {
            Table[key] = new Entry { En = en, Ar = ar, Es = es, VrchatTermOnly = vrchatTermOnly };
        }

        private static void RegisterAll()
        {
            // Tabs / chrome
            Add("tab.check", "Check", "فحص", "Revisar");
            Add("tab.fix", "Fix", "إصلاح", "Reparar");
            Add("tab.textures", "Textures", "الأنسجة", "Texturas");
            Add("header.title", "Pre-Upload Fixer", "مصلح ما قبل الرفع", "Corrector pre-subida");
            Add("header.subtitle", "VRChat avatar checks & safe fixes", "فحوصات وإصلاحات آمنة لأفاتار VRChat", "Comprobaciones y reparaciones seguras de avatares VRChat");
            Add("header.safety", "Fix All never deletes meshes, objects, or materials. Nothing on the head is removed. Rollback saves before fixes.",
                "الإصلاح الشامل لا يحذف الشبكات أو الكائنات أو المواد. لا يُزال شيء من الرأس. يتم حفظ التراجع قبل الإصلاحات.",
                "Reparar todo nunca elimina mallas, objetos ni materiales. Nada de la cabeza se elimina. El rollback se guarda antes de reparar.");
            Add("lang.label", "Language", "اللغة", "Idioma");
            Add("support.coffee", "Support on Buy Me a Coffee", "ادعم عبر Buy Me a Coffee", "Apoyar en Buy Me a Coffee");

            Add("assign.avatar", "Assign an avatar root to run checks and fixes.",
                "عيّن جذر الأفاتار لتشغيل الفحوصات والإصلاحات.",
                "Asigna la raíz del avatar para ejecutar comprobaciones y reparaciones.");
            Add("field.avatar", "Avatar", "الأفاتار", "Avatar");
            Add("btn.use_selected", "Use Selected", "استخدم المحدد", "Usar seleccionado");
            Add("tip.use_selected", "Uses the GameObject currently selected in the Hierarchy.",
                "يستخدم الكائن المحدد حالياً في الهرمية.",
                "Usa el GameObject seleccionado en la Jerarquía.");
            Add("btn.auto_detect", "Auto-Detect", "اكتشاف تلقائي", "Auto-detectar");
            Add("tip.auto_detect", "Finds a VRCAvatarDescriptor in the scene and assigns it.",
                "يبحث عن VRCAvatarDescriptor في المشهد ويعينه.",
                "Busca un VRCAvatarDescriptor en la escena y lo asigna.");

            Add("update.detected", "Update detected. Reloading...", "تم اكتشاف تحديث. جارٍ إعادة التحميل...", "Actualización detectada. Recargando...");
            Add("btn.apply_update", "Apply Update Now", "تطبيق التحديث الآن", "Aplicar actualización ahora");

            Add("rollback.banner", "Rollback point saved from before Vtool changes.",
                "تم حفظ نقطة تراجع من قبل تغييرات Vtool.",
                "Punto de rollback guardado antes de los cambios de Vtool.");
            Add("btn.rollback", "Rollback Avatar", "تراجع الأفاتار", "Revertir avatar");
            Add("tip.rollback", "Restores the avatar copy saved before Vtool changes. Does not delete your project files.",
                "يستعيد نسخة الأفاتار المحفوظة قبل تغييرات Vtool. لا يحذف ملفات المشروع.",
                "Restaura la copia del avatar guardada antes de los cambios de Vtool. No elimina archivos del proyecto.");
            Add("cap.rollback", "Restores the saved avatar copy and texture import settings if changed.",
                "يستعيد نسخة الأفاتار المحفوظة وإعدادات استيراد الأنسجة إن تغيّرت.",
                "Restaura la copia del avatar y los ajustes de texturas si cambiaron.");

            // Check tab
            Add("sec.status", "Status", "الحالة", "Estado");
            Add("sec.blockers", "Blockers ({0})", "موانع الرفع ({0})", "Bloqueos ({0})");
            Add("sec.warnings", "Warnings ({0})", "تحذيرات ({0})", "Advertencias ({0})");
            Add("sec.result", "Result", "النتيجة", "Resultado");
            Add("sec.performance", "Performance", "الأداء", "Rendimiento");
            Add("sec.vrchat", "VRChat", "VRChat", "VRChat", true);
            Add("sec.textures", "Textures", "الأنسجة", "Texturas");
            Add("result.all_ok", "All common checks passed.", "اجتازت كل الفحوصات الشائعة.", "Todas las comprobaciones comunes pasaron.");

            Add("stat.polygons", "Polygons", "المضلعات", "Polígonos");
            Add("stat.skinned", "Skinned meshes", "شبكات الجلد", "Mallas skinned");
            Add("stat.mat_slots", "Material slots", "خانات المواد", "Ranuras de material");
            Add("stat.bones", "Bones", "العظام", "Huesos");
            Add("stat.height", "Height", "الارتفاع", "Altura");
            Add("stat.physbones", "PhysBones", "PhysBones", "PhysBones", true);
            Add("stat.particles", "Particles", "الجزيئات", "Partículas");
            Add("stat.descriptor", "Descriptor", "Descriptor", "Descriptor");
            Add("stat.pipeline", "PipelineManager", "PipelineManager", "PipelineManager", true);
            Add("stat.humanoid", "Humanoid rig", "هيكل Humanoid", "Rig Humanoid");
            Add("stat.chest", "Chest bone", "عظمة الصدر", "Hueso Chest");
            Add("stat.view", "View position", "موضع الرؤية", "Posición de vista");
            Add("stat.lipsync", "Lip sync", "Lip sync", "Lip sync", true);
            Add("stat.count", "Count", "العدد", "Cantidad");
            Add("stat.4k", "4K+", "4K+", "4K+", true);
            Add("stat.over2k", "Over 2K", "أكثر من 2K", "Más de 2K");
            Add("stat.memory", "Est. memory", "الذاكرة التقريبية", "Memoria est.");
            Add("stat.nomip", "No mipmaps", "بدون mipmaps", "Sin mipmaps");
            Add("stat.ok", "OK", "حسناً", "OK");
            Add("stat.missing", "Missing", "مفقود", "Falta");
            Add("stat.not_set", "Not set", "غير مضبوط", "No configurado");

            Add("summary.blockers", "{0} upload blocker(s) and {1} warning(s) — fix before uploading.",
                "{0} مانع(ات) رفع و {1} تحذير(ات) — أصلح قبل الرفع.",
                "{0} bloqueo(s) de subida y {1} advertencia(s): repara antes de subir.");
            Add("summary.warnings", "No blockers, but {0} warning(s) to review.",
                "لا موانع، لكن هناك {0} تحذير(ات) للمراجعة.",
                "Sin bloqueos, pero hay {0} advertencia(s) por revisar.");
            Add("summary.ok", "All common checks passed. Run VRChat SDK Build & Test before uploading.",
                "اجتازت كل الفحوصات الشائعة. شغّل Build & Test في VRChat SDK قبل الرفع.",
                "Todas las comprobaciones comunes pasaron. Ejecuta Build & Test del VRChat SDK antes de subir.");

            // Fix tab
            Add("sec.quick", "Quick actions", "إجراءات سريعة", "Acciones rápidas");
            Add("fix.intro", "Fix All only adds or adjusts settings. It does not remove GameObjects, meshes, material slots, or anything on the head.",
                "الإصلاح الشامل يضيف أو يعدّل الإعدادات فقط. لا يحذف GameObjects أو الشبكات أو خانات المواد أو أي شيء على الرأس.",
                "Reparar todo solo añade o ajusta ajustes. No elimina GameObjects, mallas, ranuras de material ni nada en la cabeza.");
            Add("btn.backup", "Backup Avatar", "نسخ احتياطي للأفاتار", "Copia de seguridad del avatar");
            Add("tip.backup", "Creates a hidden duplicate in the scene you can keep as a manual backup.",
                "ينشئ نسخة مخفية في المشهد كنسخة احتياطية يدوية.",
                "Crea un duplicado oculto en la escena como copia de seguridad manual.");
            Add("cap.backup", "Creates an inactive scene copy. Does not change your original avatar.",
                "ينشئ نسخة غير نشطة في المشهد. لا يغيّر الأفاتار الأصلي.",
                "Crea una copia inactiva en la escena. No cambia tu avatar original.");
            Add("btn.fix_all", "Fix All Safe Upload Errors", "إصلاح كل أخطاء الرفع الآمنة", "Reparar todos los errores seguros de subida");
            Add("tip.fix_all", "Applies conservative fixes only. Never deletes meshes, objects, or material slots.",
                "يطبق إصلاحات محافظة فقط. لا يحذف الشبكات أو الكائنات أو خانات المواد أبداً.",
                "Aplica solo reparaciones conservadoras. Nunca elimina mallas, objetos ni ranuras de material.");
            Add("cap.fix_all", "Adds/adjusts settings only. Does not remove meshes, objects, material slots, or head content.",
                "يضيف/يعدّل الإعدادات فقط. لا يزيل الشبكات أو الكائنات أو خانات المواد أو محتوى الرأس.",
                "Solo añade/ajusta ajustes. No quita mallas, objetos, ranuras de material ni contenido de la cabeza.");
            Add("fold.individual", "Individual fixes", "إصلاحات فردية", "Reparaciones individuales");
            Add("label.optional", "Optional / changes more", "اختياري / يغيّر أكثر", "Opcional / cambia más");

            Add("btn.fix_mats", "Fix missing material slots (nearby material only)",
                "إصلاح خانات المواد الفارغة (مادة قريبة فقط)",
                "Reparar ranuras de material vacías (solo material cercano)");
            Add("tip.fix_mats", "Fills null slots by copying a nearby material on the same renderer. Never deletes slots.",
                "يملأ الخانات الفارغة بنسخ مادة قريبة على نفس العارض. لا يحذف الخانات.",
                "Rellena ranuras nulas copiando un material cercano del mismo renderer. No elimina ranuras.");
            Add("btn.add_pipeline", "Add PipelineManager", "إضافة PipelineManager", "Añadir PipelineManager");
            Add("tip.add_pipeline", "Adds PipelineManager on the avatar root if missing.",
                "يضيف PipelineManager على جذر الأفاتار إن كان مفقوداً.",
                "Añade PipelineManager en la raíz del avatar si falta.");
            Add("btn.fix_bounds", "Fix skinned mesh bounds", "إصلاح حدود الشبكات الجلدية", "Reparar bounds de mallas skinned");
            Add("tip.fix_bounds", "Recalculates SkinnedMeshRenderer local bounds so meshes cull correctly.",
                "يعيد حساب حدود SkinnedMeshRenderer لتظهر الشبكات بشكل صحيح.",
                "Recalcula los bounds locales de SkinnedMeshRenderer para un culling correcto.");
            Add("btn.fix_audio", "Fix audio (3D, volume, playOnAwake)",
                "إصلاح الصوت (ثلاثي الأبعاد، الحجم، playOnAwake)",
                "Reparar audio (3D, volumen, playOnAwake)");
            Add("tip.fix_audio", "Sets spatialBlend to 3D, caps loud volume, and turns off playOnAwake.",
                "يضع spatialBlend ثلاثي الأبعاد، يحدّ من الصوت العالي، ويعطّل playOnAwake.",
                "Pone spatialBlend en 3D, limita volumen alto y desactiva playOnAwake.");
            Add("btn.view_pos", "Align view position (only if empty)",
                "محاذاة موضع الرؤية (فقط إن كان فارغاً)",
                "Alinear posición de vista (solo si está vacía)");
            Add("tip.view_pos", "Sets ViewPosition from the head only when it is currently unset.",
                "يضبط ViewPosition من الرأس فقط عندما يكون غير مضبوط.",
                "Define ViewPosition desde la cabeza solo si aún no está configurada.");
            Add("btn.lip_sync", "Setup lip sync (only if empty)",
                "إعداد Lip sync (فقط إن كان فارغاً)",
                "Configurar Lip sync (solo si está vacío)");
            Add("tip.lip_sync", "Configures visemes on the descriptor only when lip sync is unset.",
                "يضبط visemes على الـ descriptor فقط عندما يكون Lip sync غير مضبوط.",
                "Configura visemes en el descriptor solo si Lip sync no está configurado.");

            Add("btn.reduce_pb", "Reduce PhysBones to 256", "تقليل PhysBones إلى 256", "Reducir PhysBones a 256");
            Add("btn.reduce_pb_n", "Reduce PhysBones to 256 ({0} → 256)",
                "تقليل PhysBones إلى 256 ({0} → 256)",
                "Reducir PhysBones a 256 ({0} → 256)");
            Add("tip.reduce_pb", "Removes excess VRCPhysBone scripts only. Never deletes GameObjects, bones, meshes, or anything on the head/face/hair.",
                "يزيل سكربتات VRCPhysBone الزائدة فقط. لا يحذف GameObjects أو العظام أو الشبكات أو أي شيء على الرأس/الوجه/الشعر.",
                "Quita solo scripts VRCPhysBone de más. Nunca elimina GameObjects, huesos, mallas ni nada en cabeza/cara/pelo.");
            Add("cap.reduce_pb", "Removes excess PhysBone scripts only. Head, face, and hair are never touched.",
                "يزيل سكربتات PhysBone الزائدة فقط. الرأس والوجه والشعر لا تُمس أبداً.",
                "Quita solo scripts PhysBone de más. Cabeza, cara y pelo nunca se tocan.");

            Add("btn.remove_missing", "Remove missing script slots",
                "إزالة خانات السكربت المفقودة",
                "Quitar ranuras de scripts faltantes");
            Add("tip.remove_missing", "Removes broken empty script slots only (never on the head). Does not delete meshes or child objects.",
                "يزيل خانات السكربت الفارغة المعطلة فقط (أبداً على الرأس). لا يحذف الشبكات أو الكائنات الفرعية.",
                "Quita solo ranuras de scripts rotas (nunca en la cabeza). No elimina mallas ni objetos hijos.");
            Add("btn.placeholder_mats", "Fix materials with placeholder (last resort)",
                "إصلاح المواد بعنصر نائب (حل أخير)",
                "Reparar materiales con marcador (último recurso)");
            Add("tip.placeholder_mats", "Fills empty slots with a gray placeholder. Can change how parts look.",
                "يملأ الخانات الفارغة بمادة رمادية مؤقتة. قد يغيّر مظهر الأجزاء.",
                "Rellena ranuras vacías con un material gris. Puede cambiar el aspecto.");
            Add("btn.disable_others", "Disable other avatars in scene",
                "تعطيل الأفاتارات الأخرى في المشهد",
                "Desactivar otros avatares en la escena");
            Add("tip.disable_others", "Hides other avatar roots. Your selected avatar is not changed.",
                "يخفي جذور الأفاتارات الأخرى. الأفاتار المحدد لا يتغير.",
                "Oculta otras raíces de avatar. Tu avatar seleccionado no cambia.");
            Add("btn.clear_blueprint", "Clear blueprint ID (new upload)",
                "مسح blueprint ID (رفع جديد)",
                "Borrar blueprint ID (nueva subida)");
            Add("tip.clear_blueprint", "Clears PipelineManager blueprint ID so the next upload creates a new avatar listing.",
                "يمسح blueprint ID من PipelineManager لإنشاء رفع جديد.",
                "Borra el blueprint ID de PipelineManager para una subida nueva.");

            // Textures tab
            Add("sec.tex_size", "Texture size", "حجم الأنسجة", "Tamaño de texturas");
            Add("stat.textures", "Textures", "الأنسجة", "Texturas");
            Add("stat.mem_short", "Memory", "الذاكرة", "Memoria");
            Add("field.cap_to", "Cap to", "الحد الأقصى", "Limitar a");
            Add("cap.vrchat_max", "2048 (VRChat max)", "2048 (حد VRChat الأقصى)", "2048 (máx. VRChat)");
            Add("btn.reduce_tex", "Reduce to {0}px", "تصغير إلى {0}px", "Reducir a {0}px");
            Add("tip.reduce_tex", "Lowers texture import max size. Does not delete texture assets.",
                "يخفض الحد الأقصى لحجم استيراد الأنسجة. لا يحذف ملفات الأنسجة.",
                "Reduce el tamaño máximo de importación. No elimina los assets de textura.");
            Add("cap.reduce_tex", "Changes import size only. Original files stay; use Restore to undo.",
                "يغيّر حجم الاستيراد فقط. الملفات الأصلية تبقى؛ استخدم الاستعادة للتراجع.",
                "Solo cambia el tamaño de importación. Los originales permanecen; usa Restaurar para deshacer.");
            Add("btn.restore_tex", "Restore original sizes", "استعادة الأحجام الأصلية", "Restaurar tamaños originales");
            Add("tip.restore_tex", "Restores texture import size to the source file resolution.",
                "يعيد حجم استيراد الأنسجة إلى دقة الملف الأصلي.",
                "Restaura el tamaño de importación a la resolución del archivo fuente.");
            Add("btn.mipmaps", "Enable mipmaps", "تفعيل mipmaps", "Activar mipmaps");
            Add("tip.mipmaps", "Turns on mipmaps in texture import settings for avatar textures.",
                "يفعّل mipmaps في إعدادات استيراد أنسجة الأفاتار.",
                "Activa mipmaps en los ajustes de importación de texturas del avatar.");
            Add("sec.quest", "Quest / Android", "Quest / Android", "Quest / Android", true);
            Add("quest.intro", "Quest uploads need VRChat/Mobile shaders. Duplicate materials first to keep PC versions.",
                "رفع Quest يحتاج شيدرات VRChat/Mobile. تُنسخ المواد أولاً للإبقاء على نسخة PC.",
                "Las subidas Quest necesitan shaders VRChat/Mobile. Se duplican materiales para conservar las de PC.");
            Add("stat.non_quest", "Non-Quest materials", "مواد غير متوافقة مع Quest", "Materiales no Quest");
            Add("btn.quest_convert", "Convert to Quest shaders", "تحويل إلى شيدرات Quest", "Convertir a shaders Quest");
            Add("tip.quest_convert", "Duplicates materials then switches them to VRChat/Mobile/Toon Lit.",
                "ينسخ المواد ثم يحوّلها إلى VRChat/Mobile/Toon Lit.",
                "Duplica materiales y los cambia a VRChat/Mobile/Toon Lit.");
            Add("cap.quest_convert", "Duplicates materials first so PC versions can be kept.",
                "ينسخ المواد أولاً للإبقاء على نسخ PC.",
                "Duplica materiales primero para conservar versiones PC.");

            // Dialogs common
            Add("dlg.ok", "OK", "حسناً", "OK");
            Add("dlg.cancel", "Cancel", "إلغاء", "Cancelar");
            Add("dlg.continue", "Continue", "متابعة", "Continuar");
            Add("dlg.done", "Done", "تم", "Listo");
            Add("dlg.fix", "Fix", "إصلاح", "Reparar");
            Add("dlg.reduce", "Reduce", "تقليل", "Reducir");
            Add("dlg.remove", "Remove", "إزالة", "Quitar");
            Add("dlg.disable", "Disable", "تعطيل", "Desactivar");
            Add("dlg.clear", "Clear", "مسح", "Borrar");
            Add("dlg.rollback", "Rollback", "تراجع", "Revertir");
            Add("dlg.restore", "Restore", "استعادة", "Restaurar");
            Add("dlg.enable", "Enable", "تفعيل", "Activar");
            Add("dlg.convert", "Convert", "تحويل", "Convertir");

            Add("dlg.fix_all.title", "Fix All", "إصلاح الكل", "Reparar todo");
            Add("dlg.fix_all.body", "Applies safe fixes only.\n\nA rollback copy is saved first so you can undo everything.\n\nContinue?",
                "يطبق إصلاحات آمنة فقط.\n\nتُحفظ نسخة تراجع أولاً للتراجع عن كل شيء.\n\nمتابعة؟",
                "Aplica solo reparaciones seguras.\n\nSe guarda un rollback primero para poder deshacer todo.\n\n¿Continuar?");
            Add("dlg.fix_all.result", "Material slots fixed: {0}\nPipelineManager added: {1}\nBounds fixed: {2}\nAudio fixed: {3} (playOnAwake: {4})\nView position: {5}\nLip sync: {6}\n\nRe-check the Check tab. Fix pink/broken shaders manually.",
                "خانات المواد المصلحة: {0}\nPipelineManager المضاف: {1}\nالحدود المصلحة: {2}\nالصوت المصلح: {3} (playOnAwake: {4})\nموضع الرؤية: {5}\nLip sync: {6}\n\nأعد فحص تبويب Check. أصلح الشيدرات الوردية يدوياً.",
                "Ranuras de material reparadas: {0}\nPipelineManager añadido: {1}\nBounds reparados: {2}\nAudio reparado: {3} (playOnAwake: {4})\nPosición de vista: {5}\nLip sync: {6}\n\nRevisa la pestaña Check. Repara shaders rotos/rosas manualmente.");
            Add("dlg.yes", "yes", "نعم", "sí");
            Add("dlg.no", "no", "لا", "no");
            Add("dlg.set", "set", "مضبوط", "configurado");
            Add("dlg.skipped", "skipped", "تم التخطي", "omitido");
            Add("dlg.fix_complete", "Fix Complete", "اكتمل الإصلاح", "Reparación completa");

            Add("dlg.pb.title", "PhysBones", "PhysBones", "PhysBones", true);
            Add("dlg.pb.ok_body", "This avatar has {0} PhysBone component(s), which is within the 256 limit.",
                "هذا الأفاتار لديه {0} مكوّن PhysBone، وهو ضمن حد 256.",
                "Este avatar tiene {0} componente(s) PhysBone, dentro del límite de 256.");
            Add("dlg.pb.reduce_title", "Reduce PhysBones", "تقليل PhysBones", "Reducir PhysBones");
            Add("dlg.pb.reduce_body", "VRChat blocks upload above 256 PhysBone components.\n\nCurrent: {0}\nWill remove: up to {1} PhysBone script(s)\nWill keep: at least head/face/hair PhysBones\n\nSAFETY:\n• Does NOT delete GameObjects, bones, meshes, or the head\n• Never removes anything under Head / Face / Hair\n• Only removes excess VRCPhysBone components elsewhere\n• A rollback copy is saved first\n\nContinue?",
                "VRChat يمنع الرفع فوق 256 مكوّن PhysBone.\n\nالحالي: {0}\nسيُزال: حتى {1} سكربت PhysBone\nسيُبقى: على الأقل PhysBones الرأس/الوجه/الشعر\n\nالأمان:\n• لا يحذف GameObjects أو العظام أو الشبكات أو الرأس\n• لا يزيل أي شيء تحت Head / Face / Hair\n• يزيل فقط مكوّنات VRCPhysBone الزائدة في أماكن أخرى\n• تُحفظ نسخة تراجع أولاً\n\nمتابعة؟",
                "VRChat bloquea la subida por encima de 256 componentes PhysBone.\n\nActual: {0}\nSe quitarán: hasta {1} script(s) PhysBone\nSe conservarán: al menos PhysBones de cabeza/cara/pelo\n\nSEGURIDAD:\n• NO elimina GameObjects, huesos, mallas ni la cabeza\n• Nunca quita nada bajo Head / Face / Hair\n• Solo quita componentes VRCPhysBone de más en otros sitios\n• Se guarda un rollback primero\n\n¿Continuar?");
            Add("dlg.pb.done", "Removed {0} PhysBone component(s).\nBones, meshes, and head are unchanged.\nUse Rollback Avatar if you need to undo.",
                "أُزيل {0} مكوّن PhysBone.\nالعظام والشبكات والرأس لم تتغير.\nاستخدم تراجع الأفاتار إن أردت التراجع.",
                "Se quitaron {0} componente(s) PhysBone.\nHuesos, mallas y cabeza sin cambios.\nUsa Revertir avatar si necesitas deshacer.");
            Add("dlg.pb.head_kept", "Still {0} PhysBones because head/face/hair PhysBones are protected and were not removed. Reduce body/clothing PhysBones manually if needed.",
                "ما زال هناك {0} PhysBones لأن PhysBones الرأس/الوجه/الشعر محمية ولم تُزل. قلّل PhysBones الجسم/الملابس يدوياً إن لزم.",
                "Quedan {0} PhysBones porque los de cabeza/cara/pelo están protegidos y no se quitaron. Reduce PhysBones del cuerpo/ropa a mano si hace falta.");

            Add("dlg.missing.title", "Remove Missing Scripts", "إزالة السكربتات المفقودة", "Quitar scripts faltantes");
            Add("dlg.missing.body", "This removes broken empty script slots from GameObjects.\n\nIt does NOT delete meshes or child objects.\nIt never touches the head, face, or hair.\nOnly use if you know those scripts are gone for good.\n\nContinue?",
                "يزيل خانات السكربت الفارغة المعطلة من الكائنات.\n\nلا يحذف الشبكات أو الكائنات الفرعية.\nلا يمس الرأس أو الوجه أو الشعر أبداً.\nاستخدمه فقط إن كنت متأكداً أن السكربتات ذهبت نهائياً.\n\nمتابعة؟",
                "Quita ranuras de scripts rotas de los GameObjects.\n\nNO elimina mallas ni objetos hijos.\nNunca toca cabeza, cara ni pelo.\nÚsalo solo si esos scripts ya no existen.\n\n¿Continuar?");
            Add("dlg.missing.done", "Removed {0} missing script slot(s).",
                "أُزيلت {0} خانة سكربت مفقودة.",
                "Se quitaron {0} ranura(s) de scripts faltantes.");

            Add("dlg.placeholder.title", "Placeholder Materials", "مواد مؤقتة", "Materiales marcador");
            Add("dlg.placeholder.body", "Fills empty material slots with a gray placeholder.\n\nThis can change how parts look. Prefer fixing materials manually.\n\nContinue?",
                "يملأ خانات المواد الفارغة بمادة رمادية مؤقتة.\n\nقد يغيّر المظهر. يُفضّل الإصلاح اليدوي.\n\nمتابعة؟",
                "Rellena ranuras vacías con un material gris.\n\nPuede cambiar el aspecto. Preferible reparar a mano.\n\n¿Continuar?");
            Add("dlg.placeholder.done", "Filled {0} slot(s).", "مُلئت {0} خانة.", "Se rellenaron {0} ranura(s).");

            Add("dlg.disable.title", "Disable Other Avatars", "تعطيل الأفاتارات الأخرى", "Desactivar otros avatares");
            Add("dlg.disable.body", "Hides other avatar roots in this scene.\n\nYour selected avatar is not changed.\n\nContinue?",
                "يخفي جذور الأفاتارات الأخرى في هذا المشهد.\n\nالأفاتار المحدد لا يتغير.\n\nمتابعة؟",
                "Oculta otras raíces de avatar en esta escena.\n\nTu avatar seleccionado no cambia.\n\n¿Continuar?");
            Add("dlg.disable.done", "Disabled {0} other avatar(s).",
                "عُطّل {0} أفاتار آخر.",
                "Se desactivaron {0} avatar(es) más.");

            Add("dlg.blueprint.title", "Clear Blueprint ID", "مسح Blueprint ID", "Borrar Blueprint ID");
            Add("dlg.blueprint.body", "Clears the PipelineManager blueprint ID for a fresh upload.\n\nContinue?",
                "يمسح blueprint ID من PipelineManager لرفع جديد.\n\nمتابعة؟",
                "Borra el blueprint ID de PipelineManager para una subida nueva.\n\n¿Continuar?");
            Add("dlg.blueprint.cleared", "Blueprint ID cleared.", "تم مسح Blueprint ID.", "Blueprint ID borrado.");
            Add("dlg.blueprint.nothing", "Nothing to clear.", "لا شيء للمسح.", "Nada que borrar.");

            Add("dlg.rollback.title", "Rollback Avatar", "تراجع الأفاتار", "Revertir avatar");
            Add("dlg.rollback.body", "This replaces your avatar with the copy saved before Vtool changes.\n\nTexture import settings are restored too if they were changed.\n\nContinue?",
                "يستبدل أفاتارك بالنسخة المحفوظة قبل تغييرات Vtool.\n\nتُستعاد إعدادات استيراد الأنسجة أيضاً إن تغيّرت.\n\nمتابعة؟",
                "Reemplaza tu avatar con la copia guardada antes de los cambios de Vtool.\n\nTambién restaura importación de texturas si cambiaron.\n\n¿Continuar?");
            Add("dlg.rollback.done_title", "Rollback Complete", "اكتمل التراجع", "Rollback completo");
            Add("dlg.rollback.done", "Avatar restored.", "تمت استعادة الأفاتار.", "Avatar restaurado.");

            Add("dlg.backup.title", "Backup", "نسخ احتياطي", "Copia de seguridad");
            Add("dlg.backup.done", "Created:\n{0}", "تم الإنشاء:\n{0}", "Creado:\n{0}");

            Add("dlg.tex.reduce_title", "Reduce Textures", "تصغير الأنسجة", "Reducir texturas");
            Add("dlg.tex.reduce_body", "Cap avatar textures to {0}px import size?",
                "تحديد حجم استيراد أنسجة الأفاتار إلى {0}px؟",
                "¿Limitar texturas del avatar a {0}px de importación?");
            Add("dlg.tex.reduce_done", "Reduced {0} texture(s). Use Restore to undo.",
                "صُغّرت {0} نسيج(ة). استخدم الاستعادة للتراجع.",
                "Se redujeron {0} textura(s). Usa Restaurar para deshacer.");
            Add("dlg.tex.restore_title", "Restore", "استعادة", "Restaurar");
            Add("dlg.tex.restore_body", "Restore textures to source file resolution?",
                "استعادة الأنسجة إلى دقة الملف الأصلي؟",
                "¿Restaurar texturas a la resolución del archivo fuente?");
            Add("dlg.tex.restore_done", "Restored {0} texture(s).",
                "استُعيدت {0} نسيج(ة).",
                "Se restauraron {0} textura(s).");
            Add("dlg.tex.mip_title", "Enable Mipmaps", "تفعيل Mipmaps", "Activar mipmaps");
            Add("dlg.tex.mip_body", "Changes texture import settings for textures on this avatar. Continue?",
                "يغيّر إعدادات استيراد أنسجة هذا الأفاتار. متابعة؟",
                "Cambia los ajustes de importación de texturas de este avatar. ¿Continuar?");
            Add("dlg.quest.title", "Quest Conversion", "تحويل Quest", "Conversión Quest");
            Add("dlg.quest.body", "Duplicate materials then convert to VRChat/Mobile/Toon Lit?",
                "نسخ المواد ثم التحويل إلى VRChat/Mobile/Toon Lit؟",
                "¿Duplicar materiales y convertir a VRChat/Mobile/Toon Lit?");
            Add("dlg.quest.done", "Converted {0} material slot(s).",
                "حُوّلت {0} خانة مادة.",
                "Se convirtieron {0} ranura(s) de material.");

            // Scan blockers
            Add("issue.no_descriptor", "Missing VRCAvatarDescriptor on avatar root",
                "VRCAvatarDescriptor مفقود على جذر الأفاتار",
                "Falta VRCAvatarDescriptor en la raíz del avatar");
            Add("hint.no_descriptor", "Add from VRChat SDK menu",
                "أضفه من قائمة VRChat SDK",
                "Añádelo desde el menú VRChat SDK");
            Add("issue.no_pipeline", "Missing PipelineManager on avatar root",
                "PipelineManager مفقود على جذر الأفاتار",
                "Falta PipelineManager en la raíz del avatar");
            Add("hint.no_pipeline", "Use Fix All or add via SDK",
                "استخدم الإصلاح الشامل أو أضفه عبر SDK",
                "Usa Reparar todo o añádelo vía SDK");
            Add("issue.no_humanoid", "Missing humanoid Animator on avatar root",
                "Animator من نوع Humanoid مفقود على جذر الأفاتار",
                "Falta Animator Humanoid en la raíz del avatar");
            Add("hint.no_humanoid", "Set rig to Humanoid in Import settings",
                "اضبط الـ rig إلى Humanoid في إعدادات الاستيراد",
                "Pon el rig en Humanoid en Import settings");
            Add("issue.missing_scripts", "{0} missing script reference(s)",
                "{0} مرجع سكربت مفقود",
                "{0} referencia(s) de script faltante(s)");
            Add("hint.missing_scripts", "Use Individual fixes (removes broken slots only)",
                "استخدم الإصلاحات الفردية (يزيل الخانات المعطلة فقط)",
                "Usa reparaciones individuales (solo quita ranuras rotas)");
            Add("issue.null_mats", "{0} null material slot(s)",
                "{0} خانة مادة فارغة",
                "{0} ranura(s) de material nula(s)");
            Add("hint.null_mats", "Fix All copies a nearby material on the same renderer",
                "الإصلاح الشامل ينسخ مادة قريبة على نفس العارض",
                "Reparar todo copia un material cercano del mismo renderer");
            Add("issue.broken_shaders", "{0} broken shader(s) (pink materials)",
                "{0} شيدر معطل (مواد وردية)",
                "{0} shader(s) roto(s) (materiales rosas)");
            Add("hint.broken_shaders", "Reassign shaders manually",
                "أعد تعيين الشيدرات يدوياً",
                "Reasigna shaders manualmente");
            Add("issue.missing_meshes", "{0} renderer(s) with missing mesh",
                "{0} عارض بشبكة مفقودة",
                "{0} renderer(s) con malla faltante");
            Add("hint.missing_meshes", "Reassign or remove broken renderers",
                "أعد التعيين أو أزل العوارض المعطلة",
                "Reasigna o quita renderers rotos");
            Add("issue.extreme_poly", "Extreme polygon count ({0})",
                "عدد مضلعات مرتفع جداً ({0})",
                "Conteo de polígonos extremo ({0})");
            Add("hint.extreme_poly", "Reduce in Blender or decimate",
                "قلّل في Blender أو استخدم decimate",
                "Reduce en Blender o usa decimate");
            Add("issue.physbone_limit", "Phys Bone Components: {0} — exceeds VRChat limit (256)",
                "مكوّنات Phys Bone: {0} — تتجاوز حد VRChat (256)",
                "Componentes Phys Bone: {0} — supera el límite de VRChat (256)");
            Add("hint.physbone_limit", "Use Individual fixes → Reduce PhysBones to 256 (scripts only; head/face/hair never touched)",
                "استخدم الإصلاحات الفردية → تقليل PhysBones إلى 256 (سكربتات فقط؛ الرأس/الوجه/الشعر لا تُمس)",
                "Usa reparaciones individuales → Reducir PhysBones a 256 (solo scripts; cabeza/cara/pelo intactos)");

            // Scan warnings
            Add("issue.no_chest", "Humanoid rig missing Chest bone mapping",
                "هيكل Humanoid بلا تعيين عظمة Chest",
                "Rig Humanoid sin mapeo de hueso Chest");
            Add("hint.no_chest", "Map Chest in Rig configuration",
                "عيّن Chest في إعدادات الـ Rig",
                "Mapea Chest en la configuración del Rig");
            Add("issue.no_view", "View position not set on descriptor",
                "موضع الرؤية غير مضبوط على الـ descriptor",
                "Posición de vista no configurada en el descriptor");
            Add("hint.fix_if_empty", "Fix All sets it only when empty",
                "الإصلاح الشامل يضبطه فقط إن كان فارغاً",
                "Reparar todo lo configura solo si está vacío");
            Add("issue.no_lipsync", "Lip sync / visemes not configured",
                "Lip sync / visemes غير مضبوط",
                "Lip sync / visemes no configurados");
            Add("issue.root_scale", "Avatar root scale is not (1,1,1)",
                "مقياس جذر الأفاتار ليس (1,1,1)",
                "La escala de la raíz del avatar no es (1,1,1)");
            Add("hint.root_scale", "Can cause IK issues — normalize if needed",
                "قد يسبب مشاكل IK — طبّع إن لزم",
                "Puede causar problemas de IK — normaliza si hace falta");
            Add("issue.neg_scale", "{0} transform(s) with negative scale",
                "{0} تحويل بمقياس سالب",
                "{0} transform(s) con escala negativa");
            Add("hint.neg_scale", "Can invert normals and break uploads",
                "قد يعكس النورملز ويعطل الرفع",
                "Puede invertir normales y romper la subida");
            Add("issue.nonunit_scale", "{0} transform(s) with non-unit scale",
                "{0} تحويل بمقياس غير واحد",
                "{0} transform(s) con escala distinta de 1");
            Add("hint.nonunit_scale", "May cause animation/IK issues",
                "قد يسبب مشاكل حركة/IK",
                "Puede causar problemas de animación/IK");
            Add("issue.high_poly", "High polygon count ({0}) — Poor rank on PC",
                "عدد مضلعات مرتفع ({0}) — رتبة Poor على PC",
                "Alto conteo de polígonos ({0}) — rango Poor en PC");
            Add("hint.high_poly", "Decimate or optimize mesh",
                "قلّل أو حسّن الشبكة",
                "Decima u optimiza la malla");
            Add("issue.quest_poly", "Over Quest limit ({0} tris)",
                "فوق حد Quest ({0} مثلث)",
                "Sobre el límite Quest ({0} tris)");
            Add("hint.quest_poly", "Required for Android/Quest uploads",
                "مطلوب لرفع Android/Quest",
                "Requerido para subidas Android/Quest");
            Add("issue.skinned_many", "{0} skinned meshes (8+ hurts performance)",
                "{0} شبكات جلدية (8+ يضر الأداء)",
                "{0} mallas skinned (8+ empeora el rendimiento)");
            Add("hint.skinned_many", "Merge meshes if possible",
                "ادمج الشبكات إن أمكن",
                "Fusiona mallas si es posible");
            Add("issue.mats_many", "{0} material slots (16+ hurts performance)",
                "{0} خانة مادة (16+ يضر الأداء)",
                "{0} ranuras de material (16+ empeora el rendimiento)");
            Add("hint.mats_many", "Atlas textures / merge materials",
                "اجمع الأنسجة / ادمج المواد",
                "Haz atlas / fusiona materiales");
            Add("issue.tex_4k", "{0} texture(s) at 4K+",
                "{0} نسيج بدقة 4K+",
                "{0} textura(s) en 4K+");
            Add("hint.tex_4k", "Reduce to 2K in Textures tab",
                "قلّل إلى 2K في تبويب Textures",
                "Reduce a 2K en la pestaña Textures");
            Add("issue.tex_2k", "{0} texture(s) over 2K",
                "{0} نسيج فوق 2K",
                "{0} textura(s) sobre 2K");
            Add("hint.tex_2k", "VRChat recommends 2K max",
                "VRChat يوصي بحد أقصى 2K",
                "VRChat recomienda 2K máximo");
            Add("issue.tex_mem", "High texture memory (~{0} MB)",
                "ذاكرة أنسجة مرتفعة (~{0} MB)",
                "Alta memoria de texturas (~{0} MB)");
            Add("hint.tex_mem", "Can fail security checks",
                "قد يفشل فحوصات الأمان",
                "Puede fallar comprobaciones de seguridad");
            Add("issue.no_mip", "{0} texture(s) missing mipmaps",
                "{0} نسيج بدون mipmaps",
                "{0} textura(s) sin mipmaps");
            Add("hint.use_tex_tab", "Use Textures tab",
                "استخدم تبويب Textures",
                "Usa la pestaña Textures");
            Add("issue.dynbone", "{0} legacy Dynamic Bone(s)",
                "{0} Dynamic Bone قديم",
                "{0} Dynamic Bone(s) legado(s)");
            Add("hint.dynbone", "Migrate to PhysBones",
                "انقل إلى PhysBones",
                "Migra a PhysBones");
            Add("issue.pb_poor", "{0} PhysBones (32+ is Very Poor on PC)",
                "{0} PhysBones (32+ رتبة Very Poor على PC)",
                "{0} PhysBones (32+ es Very Poor en PC)");
            Add("hint.pb_poor", "Consider combining or reducing PhysBones",
                "فكّر بدمج أو تقليل PhysBones",
                "Considera combinar o reducir PhysBones");
            Add("issue.bad_audio", "{0} audio source(s) need 3D spatialization",
                "{0} مصدر صوت يحتاج تموضعاً ثلاثي الأبعاد",
                "{0} fuente(s) de audio necesitan espacialización 3D");
            Add("hint.fix_all_audio", "Fix All corrects audio",
                "الإصلاح الشامل يصحح الصوت",
                "Reparar todo corrige el audio");
            Add("issue.play_awake", "{0} audio plays on awake",
                "{0} صوت يعمل عند التشغيل",
                "{0} audio se reproduce al activar");
            Add("hint.play_awake", "Fix All disables playOnAwake",
                "الإصلاح الشامل يعطّل playOnAwake",
                "Reparar todo desactiva playOnAwake");
            Add("issue.particles", "{0} particle systems (16+ hurts performance)",
                "{0} نظام جزيئات (16+ يضر الأداء)",
                "{0} sistemas de partículas (16+ empeora el rendimiento)");
            Add("hint.particles", "Reduce particle count",
                "قلّل عدد الجزيئات",
                "Reduce la cantidad de partículas");
            Add("issue.other_avatars", "{0} other avatar(s) active in scene",
                "{0} أفاتار آخر نشط في المشهد",
                "{0} otro(s) avatar(es) activo(s) en la escena");
            Add("hint.other_avatars", "Use Individual fixes to hide them",
                "استخدم الإصلاحات الفردية لإخفائها",
                "Usa reparaciones individuales para ocultarlos");
            Add("issue.quest_mats", "{0} material(s) not Quest-compatible",
                "{0} مادة غير متوافقة مع Quest",
                "{0} material(es) no compatible(s) con Quest");
            Add("hint.quest_mats", "Use Quest conversion in Textures tab",
                "استخدم تحويل Quest في تبويب Textures",
                "Usa la conversión Quest en la pestaña Textures");
            Add("issue.height", "Unusual avatar height ({0}m)",
                "ارتفاع أفاتار غير معتاد ({0}m)",
                "Altura de avatar inusual ({0}m)");
            Add("hint.height", "Check view position and scale",
                "تحقق من موضع الرؤية والمقياس",
                "Revisa posición de vista y escala");
        }
    }
}
