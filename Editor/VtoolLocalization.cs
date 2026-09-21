using System.Collections.Generic;
using UnityEditor;

namespace XVR.Tools
{
    public enum VtoolLanguage
    {
        English = 0,
        Arabic = 1,
        Spanish = 2,
        French = 3
    }

    public static class VtoolLocalization
    {
        private const string PrefsKey = "com.vtool.autofixer.language";

        private struct Entry
        {
            public string En;
            public string Ar;
            public string Es;
            public string Fr;
            public bool VrchatTermOnly;
        }

        private static readonly Dictionary<string, Entry> Table = new Dictionary<string, Entry>();
        private static bool ready;

        public static VtoolLanguage Language
        {
            get
            {
                int v = EditorPrefs.GetInt(PrefsKey, (int)VtoolLanguage.English);
                if (v < 0 || v > (int)VtoolLanguage.French)
                    return VtoolLanguage.English;
                return (VtoolLanguage)v;
            }
            set => EditorPrefs.SetInt(PrefsKey, (int)value);
        }

        public static string[] LanguageDisplayNames => new[]
        {
            "English",
            "Arabic (" + VtoolArabicImgui.Fix("العربية") + ")",
            "Español (Spanish)",
            "Français (French)"
        };

        private static string LocalOf(Entry e)
        {
            switch (Language)
            {
                case VtoolLanguage.Arabic: return e.Ar;
                case VtoolLanguage.Spanish: return e.Es;
                case VtoolLanguage.French: return e.Fr;
                default: return e.En;
            }
        }

        // All IMGUI UI text should go through T or TF so Arabic is shaped + reversed for LTR.
        public static string T(string key)
        {
            EnsureReady();
            if (!Table.TryGetValue(key, out var e))
                return Prepare(key);

            if (e.VrchatTermOnly || Language == VtoolLanguage.English)
                return e.En;

            string local = LocalOf(e);
            if (string.IsNullOrEmpty(local) || local == e.En)
                return e.En;

            if (Language == VtoolLanguage.Arabic)
            {
                // Shape Arabic only, then append English — never Fix the combined string
                // (that would reverse chunk order and put English first).
                return VtoolArabicImgui.Fix(local) + " (" + e.En + ")";
            }

            return local + " (" + e.En + ")";
        }

        public static string TF(string key, params object[] args)
        {
            EnsureReady();
            if (!Table.TryGetValue(key, out var e))
                return Prepare(key);

            try
            {
                if (e.VrchatTermOnly || Language == VtoolLanguage.English)
                    return string.Format(e.En, args);

                string local = LocalOf(e);
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

        // Native EditorUtility.DisplayDialog — shape Arabic for connected letters, do not reverse.
        public static string TDialog(string key)
        {
            EnsureReady();
            if (!Table.TryGetValue(key, out var e))
                return PrepareDialog(key);

            if (e.VrchatTermOnly || Language == VtoolLanguage.English)
                return e.En;

            string local = LocalOf(e);
            if (string.IsNullOrEmpty(local) || local == e.En)
                return e.En;

            if (Language == VtoolLanguage.Arabic)
                return VtoolArabicImgui.ShapeForNativeUi(local) + " (" + e.En + ")";

            return local + " (" + e.En + ")";
        }

        public static string TFDialog(string key, params object[] args)
        {
            EnsureReady();
            if (!Table.TryGetValue(key, out var e))
                return PrepareDialog(key);

            try
            {
                if (e.VrchatTermOnly || Language == VtoolLanguage.English)
                    return string.Format(e.En, args);

                string local = LocalOf(e);
                if (string.IsNullOrEmpty(local) || local == e.En)
                    return string.Format(e.En, args);

                string formattedLocal = string.Format(local, args);
                string formattedEn = string.Format(e.En, args);

                if (Language == VtoolLanguage.Arabic)
                    return VtoolArabicImgui.ShapeForNativeUi(formattedLocal) + " (" + formattedEn + ")";

                return formattedLocal + " (" + formattedEn + ")";
            }
            catch
            {
                return TDialog(key);
            }
        }

        public static string PrepareDialog(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (Language == VtoolLanguage.Arabic || ContainsArabic(text))
                return VtoolArabicImgui.ShapeForNativeUi(text);
            return text;
        }

        public static string Raw(string key)
        {
            EnsureReady();
            if (!Table.TryGetValue(key, out var e))
                return Prepare(key);
            switch (Language)
            {
                case VtoolLanguage.Arabic:
                    return Prepare(string.IsNullOrEmpty(e.Ar) ? e.En : e.Ar);
                case VtoolLanguage.Spanish:
                    return string.IsNullOrEmpty(e.Es) ? e.En : e.Es;
                case VtoolLanguage.French:
                    return string.IsNullOrEmpty(e.Fr) ? e.En : e.Fr;
                default:
                    return e.En;
            }
        }

        public static string Prepare(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (Language == VtoolLanguage.Arabic || ContainsArabic(text))
                return VtoolArabicImgui.Fix(text);
            return text;
        }

        private static bool ContainsArabic(string s)
        {
            foreach (char c in s)
            {
                if (c >= '\u0600' && c <= '\u06FF') return true;
                if (c >= '\u0750' && c <= '\u077F') return true;
                if (c >= '\uFB50' && c <= '\uFDFF') return true;
                if (c >= '\uFE70' && c <= '\uFEFF') return true;
            }
            return false;
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

        private static void Add(string key, string en, string ar, string es, string fr, bool vrchatTermOnly = false)
        {
            Table[key] = new Entry { En = en, Ar = ar, Es = es, Fr = fr, VrchatTermOnly = vrchatTermOnly };
        }

        private static void RegisterAll()
        {

            Add("tab.check", "Check", "فحص", "Revisar", "Vérifier");
            Add("tab.fix", "Fix", "إصلاح", "Reparar", "Réparer");
            Add("tab.textures", "Textures", "الأنسجة", "Texturas", "Textures");
            Add("header.title", "Pre-Upload Fixer", "مصلح ما قبل الرفع", "Corrector pre-subida", "Correcteur pré-upload");
            Add("header.subtitle", "VRChat avatar checks & safe fixes", "فحوصات وإصلاحات آمنة لأفاتار VRChat", "Comprobaciones y reparaciones seguras de avatares VRChat", "Contrôles et réparations sûres d'avatars VRChat");
            Add("header.safety", "Fix All never deletes meshes, objects, or materials. Nothing on the head is removed. Rollback saves before fixes.", "الإصلاح الشامل لا يحذف الشبكات أو الكائنات أو المواد. لا يُزال شيء من الرأس. يتم حفظ التراجع قبل الإصلاحات.", "Reparar todo nunca elimina mallas, objetos ni materiales. Nada de la cabeza se elimina. El rollback se guarda antes de reparar.", "Réparer tout ne supprime jamais les maillages, objets ou matériaux. Rien sur la tête n'est retiré. Un point de restauration est enregistré avant les réparations.");
            Add("lang.label", "Language", "اللغة", "Idioma", "Langue");
            Add("support.coffee", "Support on Ko-fi", "ادعم عبر Ko-fi", "Apoyar en Ko-fi", "Soutenir sur Ko-fi");
            Add("assign.avatar", "Assign an avatar root to run checks and fixes.", "عيّن جذر الأفاتار لتشغيل الفحوصات والإصلاحات.", "Asigna la raíz del avatar para ejecutar comprobaciones y reparaciones.", "Assignez la racine de l'avatar pour lancer les contrôles et réparations.");
            Add("field.avatar", "Avatar", "الأفاتار", "Avatar", "Avatar");
            Add("btn.use_selected", "Use Selected", "استخدم المحدد", "Usar seleccionado", "Utiliser la sélection");
            Add("tip.use_selected", "Uses the GameObject currently selected in the Hierarchy.", "يستخدم الكائن المحدد حالياً في الهرمية.", "Usa el GameObject seleccionado en la Jerarquía.", "Utilise le GameObject actuellement sélectionné dans la Hiérarchie.");
            Add("btn.auto_detect", "Auto-Detect", "اكتشاف تلقائي", "Auto-detectar", "Détection auto");
            Add("tip.auto_detect", "Finds a VRCAvatarDescriptor in the scene and assigns it.", "يبحث عن VRCAvatarDescriptor في المشهد ويعينه.", "Busca un VRCAvatarDescriptor en la escena y lo asigna.", "Trouve un VRCAvatarDescriptor dans la scène et l'assigne.");
            Add("update.detected", "Update detected. Reloading...", "تم اكتشاف تحديث. جارٍ إعادة التحميل...", "Actualización detectada. Recargando...", "Mise à jour détectée. Rechargement…");
            Add("btn.apply_update", "Apply Update Now", "تطبيق التحديث الآن", "Aplicar actualización ahora", "Appliquer la mise à jour");
            Add("update.dialog_title", "Vtool Update", "تحديث Vtool", "Actualización de Vtool", "Mise à jour Vtool");
            Add("update.already_latest", "Already on the latest installed package.", "أنت بالفعل على أحدث حزمة مثبتة.", "Ya tienes el paquete instalado más reciente.", "Vous avez déjà le dernier package installé.");
            Add("update.reload_body", "A new Vtool package was installed while Unity was open.\n\nUnity will refresh and reload now so the update takes effect.", "تم تثبيت حزمة Vtool جديدة أثناء فتح Unity.\n\nسيتم تحديث Unity وإعادة التحميل الآن لتطبيق التحديث.", "Se instaló un paquete nuevo de Vtool con Unity abierto.\n" +
                "\n" +
                "Unity se actualizará y recargará ahora para aplicar el cambio.", "Un nouveau package Vtool a été installé pendant que Unity était ouvert.\n" +
                "\n" +
                "Unity va actualiser et recharger maintenant pour appliquer la mise à jour.");
            Add("rollback.banner", "Rollback point saved from before Vtool changes.", "تم حفظ نقطة تراجع من قبل تغييرات Vtool.", "Punto de rollback guardado antes de los cambios de Vtool.", "Point de restauration enregistré avant les changements Vtool.");
            Add("rollback.none", "No rollback snapshot yet. Use Backup or Fix All first.", "لا توجد نقطة تراجع بعد. استخدم النسخ الاحتياطي أو إصلاح الكل أولاً.", "Aún no hay snapshot de rollback. Usa Copia o Fix All primero.", "Pas encore de snapshot de restauration. Utilisez d'abord Sauvegarde ou Réparer tout.");
            Add("btn.rollback", "Rollback Avatar", "تراجع الأفاتار", "Revertir avatar", "Restaurer l'avatar");
            Add("tip.rollback", "Restores the avatar copy saved before Vtool changes. Does not delete your project files.", "يستعيد نسخة الأفاتار المحفوظة قبل تغييرات Vtool. لا يحذف ملفات المشروع.", "Restaura la copia del avatar guardada antes de los cambios de Vtool. No elimina archivos del proyecto.", "Restaure la copie de l'avatar enregistrée avant les changements Vtool. Ne supprime pas les fichiers du projet.");
            Add("cap.rollback", "Restores the saved avatar copy and texture import settings if changed.", "يستعيد نسخة الأفاتار المحفوظة وإعدادات استيراد الأنسجة إن تغيّرت.", "Restaura la copia del avatar y los ajustes de texturas si cambiaron.", "Restaure la copie de l'avatar et les réglages d'import des textures s'ils ont changé.");
            Add("sec.status", "Status", "الحالة", "Estado", "État");
            Add("fold.check_details", "Details (performance / VRChat / textures)", "التفاصيل (الأداء / VRChat / الأنسجة)", "Detalles (rendimiento / VRChat / texturas)", "Détails (performances / VRChat / textures)");
            Add("btn.copy_errors", "Copy Error Codes", "نسخ أكواد الأخطاء", "Copiar códigos de error", "Copier les codes d'erreur");
            Add("tip.copy_errors", "Copies blocker/warning codes plus scan stats to the clipboard so you can paste them when asking for help.", "ينسخ أكواد الموانع والتحذيرات مع إحصائيات الفحص إلى الحافظة للصقها عند طلب المساعدة.", "Copia códigos de bloqueo/aviso y estadísticas al portapapeles para pegarlos al pedir ayuda.", "Copie les codes de blocage/avertissement et les stats d'analyse dans le presse-papiers pour les coller quand vous demandez de l'aide.");
            Add("cap.copy_errors", "Paste this report when asking what is wrong with the avatar or Vtool.", "الصق هذا التقرير عند السؤال عما خطأ في الأفاتار أو Vtool.", "Pega este informe al preguntar qué falla en el avatar o en Vtool.", "Collez ce rapport quand vous demandez ce qui ne va pas avec l'avatar ou Vtool.");
            Add("dlg.copy_errors.title", "Copied", "تم النسخ", "Copiado", "Copié");
            Add("dlg.copy_errors.body", "Error codes and scan details were copied to the clipboard. Paste them when reporting a problem.", "تم نسخ أكواد الأخطاء وتفاصيل الفحص إلى الحافظة. الصقها عند الإبلاغ عن مشكلة.", "Los códigos de error y el escaneo se copiaron al portapapeles. Pégalos al reportar un problema.", "Les codes d'erreur et les détails d'analyse ont été copiés dans le presse-papiers. Collez-les pour signaler un problème.");
            Add("sec.blockers", "Blockers ({0})", "موانع الرفع ({0})", "Bloqueos ({0})", "Blocages ({0})");
            Add("sec.warnings", "Warnings ({0})", "تحذيرات ({0})", "Advertencias ({0})", "Avertissements ({0})");
            Add("sec.result", "Result", "النتيجة", "Resultado", "Résultat");
            Add("sec.performance", "Performance", "الأداء", "Rendimiento", "Performances");
            Add("sec.vrchat", "VRChat", "VRChat", "VRChat", "VRChat", true);
            Add("sec.textures", "Textures", "الأنسجة", "Texturas", "Textures");
            Add("result.all_ok", "All common checks passed.", "اجتازت كل الفحوصات الشائعة.", "Todas las comprobaciones comunes pasaron.", "Tous les contrôles courants sont OK.");
            Add("result.no_blockers", "No blockers.", "لا توجد موانع رفع.", "Sin bloqueos.", "Aucun blocage.");
            Add("result.no_warnings", "No warnings.", "لا توجد تحذيرات.", "Sin advertencias.", "Aucun avertissement.");
            Add("result.has_issues", "Issues listed above.", "المشكلات مدرجة أعلاه.", "Problemas listados arriba.", "Problèmes listés ci-dessus.");
            Add("stat.polygons", "Polygons", "المضلعات", "Polígonos", "Polygones");
            Add("stat.skinned", "Skinned meshes", "شبكات الجلد", "Mallas skinned", "Maillages skinned");
            Add("stat.mat_slots", "Material slots", "خانات المواد", "Ranuras de material", "Emplacements matériaux");
            Add("stat.bones", "Bones", "العظام", "Huesos", "Os");
            Add("stat.height", "Height", "الارتفاع", "Altura", "Hauteur");
            Add("stat.physbones", "PhysBones", "PhysBones", "PhysBones", "PhysBones", true);
            Add("stat.particles", "Particles", "الجزيئات", "Partículas", "Particules");
            Add("stat.descriptor", "Descriptor", "Descriptor", "Descriptor", "Descriptor");
            Add("stat.pipeline", "PipelineManager", "PipelineManager", "PipelineManager", "PipelineManager", true);
            Add("stat.humanoid", "Humanoid rig", "هيكل Humanoid", "Rig Humanoid", "Rig Humanoid");
            Add("stat.chest", "Chest bone", "عظمة الصدر", "Hueso Chest", "Os Chest");
            Add("stat.view", "View position", "موضع الرؤية", "Posición de vista", "Position de vue");
            Add("stat.lipsync", "Lip sync", "Lip sync", "Lip sync", "Lip sync", true);
            Add("stat.count", "Count", "العدد", "Cantidad", "Nombre");
            Add("stat.4k", "4K+", "4K+", "4K+", "4K+", true);
            Add("stat.over2k", "Over 2K", "أكثر من 2K", "Más de 2K", "Plus de 2K");
            Add("stat.memory", "Est. memory", "الذاكرة التقريبية", "Memoria est.", "Mémoire est.");
            Add("stat.nomip", "No mipmaps", "بدون mipmaps", "Sin mipmaps", "Sans mipmaps");
            Add("stat.ok", "OK", "حسناً", "OK", "OK");
            Add("stat.missing", "Missing", "مفقود", "Falta", "Manquant");
            Add("stat.not_set", "Not set", "غير مضبوط", "No configurado", "Non défini");
            Add("summary.blockers", "{0} upload blocker(s) and {1} warning(s) — fix before uploading.", "{0} مانع(ات) رفع و {1} تحذير(ات) — أصلح قبل الرفع.", "{0} bloqueo(s) de subida y {1} advertencia(s): repara antes de subir.", "{0} blocage(s) d'upload et {1} avertissement(s) — réparez avant d'uploader.");
            Add("summary.warnings", "No blockers, but {0} warning(s) to review.", "لا موانع، لكن هناك {0} تحذير(ات) للمراجعة.", "Sin bloqueos, pero hay {0} advertencia(s) por revisar.", "Aucun blocage, mais {0} avertissement(s) à revoir.");
            Add("summary.ok", "All common checks passed. Run VRChat SDK Build & Test before uploading.", "اجتازت كل الفحوصات الشائعة. شغّل Build & Test في VRChat SDK قبل الرفع.", "Todas las comprobaciones comunes pasaron. Ejecuta Build & Test del VRChat SDK antes de subir.", "Tous les contrôles courants sont OK. Lancez Build & Test du VRChat SDK avant d'uploader.");
            Add("sec.quick", "Quick actions", "إجراءات سريعة", "Acciones rápidas", "Actions rapides");
            Add("fix.intro", "Fix All only adds or adjusts settings. It does not remove GameObjects, meshes, material slots, or anything on the head.", "الإصلاح الشامل يضيف أو يعدّل الإعدادات فقط. لا يحذف GameObjects أو الشبكات أو خانات المواد أو أي شيء على الرأس.", "Reparar todo solo añade o ajusta ajustes. No elimina GameObjects, mallas, ranuras de material ni nada en la cabeza.", "Réparer tout ajoute ou ajuste seulement des réglages. Cela ne supprime pas de GameObjects, maillages, emplacements matériaux, ni quoi que ce soit sur la tête.");
            Add("btn.backup", "Backup Avatar", "نسخ احتياطي للأفاتار", "Copia de seguridad del avatar", "Sauvegarder l'avatar");
            Add("tip.backup", "Creates a hidden duplicate in the scene you can keep as a manual backup.", "ينشئ نسخة مخفية في المشهد كنسخة احتياطية يدوية.", "Crea un duplicado oculto en la escena como copia de seguridad manual.", "Crée un doublon caché dans la scène comme sauvegarde manuelle.");
            Add("cap.backup", "Creates an inactive scene copy. Does not change your original avatar.", "ينشئ نسخة غير نشطة في المشهد. لا يغيّر الأفاتار الأصلي.", "Crea una copia inactiva en la escena. No cambia tu avatar original.", "Crée une copie inactive dans la scène. Ne modifie pas votre avatar d'origine.");
            Add("btn.fix_all", "Fix All Safe Upload Errors", "إصلاح كل أخطاء الرفع الآمنة", "Reparar todos los errores seguros de subida", "Réparer toutes les erreurs d'upload sûres");
            Add("tip.fix_all", "Applies conservative fixes only. Never deletes meshes, objects, or material slots.", "يطبق إصلاحات محافظة فقط. لا يحذف الشبكات أو الكائنات أو خانات المواد أبداً.", "Aplica solo reparaciones conservadoras. Nunca elimina mallas, objetos ni ranuras de material.", "Applique uniquement des réparations prudentes. Ne supprime jamais maillages, objets ou emplacements matériaux.");
            Add("cap.fix_all", "Adds/adjusts settings only. Does not remove meshes, objects, material slots, or head content.", "يضيف/يعدّل الإعدادات فقط. لا يزيل الشبكات أو الكائنات أو خانات المواد أو محتوى الرأس.", "Solo añade/ajusta ajustes. No quita mallas, objetos, ranuras de material ni contenido de la cabeza.", "Ajoute/ajuste seulement des réglages. Ne retire pas maillages, objets, emplacements matériaux ni contenu de la tête.");
            Add("fold.individual", "Individual fixes", "إصلاحات فردية", "Reparaciones individuales", "Réparations individuelles");
            Add("label.optional", "Optional / changes more", "اختياري / يغيّر أكثر", "Opcional / cambia más", "Optionnel / change davantage");
            Add("btn.fix_mats", "Fix missing material slots (nearby material only)", "إصلاح خانات المواد الفارغة (مادة قريبة فقط)", "Reparar ranuras de material vacías (solo material cercano)", "Réparer les emplacements matériaux manquants (matériau proche seulement)");
            Add("tip.fix_mats", "Fills null slots by copying a nearby material on the same renderer. Never deletes slots.", "يملأ الخانات الفارغة بنسخ مادة قريبة على نفس العارض. لا يحذف الخانات.", "Rellena ranuras nulas copiando un material cercano del mismo renderer. No elimina ranuras.", "Remplit les emplacements nuls en copiant un matériau proche du même renderer. Ne supprime jamais d'emplacements.");
            Add("btn.add_pipeline", "Add PipelineManager", "إضافة PipelineManager", "Añadir PipelineManager", "Ajouter PipelineManager");
            Add("tip.add_pipeline", "Adds PipelineManager on the avatar root if missing.", "يضيف PipelineManager على جذر الأفاتار إن كان مفقوداً.", "Añade PipelineManager en la raíz del avatar si falta.", "Ajoute PipelineManager sur la racine de l'avatar s'il manque.");
            Add("btn.fix_bounds", "Fix skinned mesh bounds", "إصلاح حدود الشبكات الجلدية", "Reparar bounds de mallas skinned", "Réparer les bounds des maillages skinned");
            Add("tip.fix_bounds", "Expands SkinnedMeshRenderer local bounds only. Skips head/face/hair meshes so they are not culled.", "يوسّع حدود SkinnedMeshRenderer المحلية فقط. يتخطى شبكات الرأس/الوجه/الشعر حتى لا تُقص.", "Solo amplía los bounds locales de SkinnedMeshRenderer. Omite mallas de cabeza/cara/pelo para no cullarlas.", "Agrandit seulement les bounds locaux des SkinnedMeshRenderer. Ignore tête/visage/cheveux pour éviter le culling.");
            Add("btn.fix_audio", "Fix audio (3D, volume, playOnAwake)", "إصلاح الصوت (ثلاثي الأبعاد، الحجم، playOnAwake)", "Reparar audio (3D, volumen, playOnAwake)", "Réparer l'audio (3D, volume, playOnAwake)");
            Add("tip.fix_audio", "Sets spatialBlend to 3D, caps loud volume, and turns off playOnAwake.", "يضع spatialBlend ثلاثي الأبعاد، يحدّ من الصوت العالي، ويعطّل playOnAwake.", "Pone spatialBlend en 3D, limita volumen alto y desactiva playOnAwake.", "Met spatialBlend en 3D, limite le volume trop fort et désactive playOnAwake.");
            Add("btn.view_pos", "Align view position (only if empty)", "محاذاة موضع الرؤية (فقط إن كان فارغاً)", "Alinear posición de vista (solo si está vacía)", "Aligner la position de vue (seulement si vide)");
            Add("tip.view_pos", "Sets ViewPosition from the head only when it is currently unset.", "يضبط ViewPosition من الرأس فقط عندما يكون غير مضبوط.", "Define ViewPosition desde la cabeza solo si aún no está configurada.", "Définit ViewPosition depuis la tête seulement si elle n'est pas encore configurée.");
            Add("btn.lip_sync", "Setup lip sync (only if empty)", "إعداد Lip sync (فقط إن كان فارغاً)", "Configurar Lip sync (solo si está vacío)", "Configurer le lip sync (seulement si vide)");
            Add("tip.lip_sync", "Configures visemes on the descriptor only when lip sync is unset.", "يضبط visemes على الـ descriptor فقط عندما يكون Lip sync غير مضبوط.", "Configura visemes en el descriptor solo si Lip sync no está configurado.", "Configure les visemes sur le descriptor seulement si le lip sync n'est pas défini.");
            Add("btn.reduce_pb", "Reduce PhysBones to 256", "تقليل PhysBones إلى 256", "Reducir PhysBones a 256", "Réduire les PhysBones à 256");
            Add("btn.reduce_pb_n", "Reduce PhysBones to 256 ({0} → 256)", "تقليل PhysBones إلى 256 ({0} → 256)", "Reducir PhysBones a 256 ({0} → 256)", "Réduire les PhysBones à 256 ({0} → 256)");
            Add("tip.reduce_pb", "Removes excess VRCPhysBone scripts only. Never deletes GameObjects, bones, meshes, or anything on the head/face/hair.", "يزيل سكربتات VRCPhysBone الزائدة فقط. لا يحذف GameObjects أو العظام أو الشبكات أو أي شيء على الرأس/الوجه/الشعر.", "Quita solo scripts VRCPhysBone de más. Nunca elimina GameObjects, huesos, mallas ni nada en cabeza/cara/pelo.", "Retire seulement les scripts VRCPhysBone en trop. Ne supprime jamais GameObjects, os, maillages, ni quoi que ce soit sur tête/visage/cheveux.");
            Add("cap.reduce_pb", "Removes excess PhysBone scripts only. Head, face, and hair are never touched.", "يزيل سكربتات PhysBone الزائدة فقط. الرأس والوجه والشعر لا تُمس أبداً.", "Quita solo scripts PhysBone de más. Cabeza, cara y pelo nunca se tocan.", "Retire seulement les scripts PhysBone en trop. Tête, visage et cheveux ne sont jamais touchés.");
            Add("btn.remove_missing", "Remove missing script slots", "إزالة خانات السكربت المفقودة", "Quitar ranuras de scripts faltantes", "Retirer les emplacements de scripts manquants");
            Add("tip.remove_missing", "Removes broken empty script slots only (never on the head). Does not delete meshes or child objects.", "يزيل خانات السكربت الفارغة المعطلة فقط (أبداً على الرأس). لا يحذف الشبكات أو الكائنات الفرعية.", "Quita solo ranuras de scripts rotas (nunca en la cabeza). No elimina mallas ni objetos hijos.", "Retire seulement les emplacements de scripts cassés (jamais sur la tête). Ne supprime pas maillages ni objets enfants.");
            Add("btn.placeholder_mats", "Fix materials with placeholder (last resort)", "إصلاح المواد بعنصر نائب (حل أخير)", "Reparar materiales con marcador (último recurso)", "Réparer les matériaux avec un placeholder (dernier recours)");
            Add("tip.placeholder_mats", "Fills empty slots with a gray placeholder. Can change how parts look.", "يملأ الخانات الفارغة بمادة رمادية مؤقتة. قد يغيّر مظهر الأجزاء.", "Rellena ranuras vacías con un material gris. Puede cambiar el aspecto.", "Remplit les emplacements vides avec un placeholder gris. Peut changer l'apparence.");
            Add("btn.disable_others", "Disable other avatars in scene", "تعطيل الأفاتارات الأخرى في المشهد", "Desactivar otros avatares en la escena", "Désactiver les autres avatars de la scène");
            Add("tip.disable_others", "Hides other avatar roots. Your selected avatar is not changed.", "يخفي جذور الأفاتارات الأخرى. الأفاتار المحدد لا يتغير.", "Oculta otras raíces de avatar. Tu avatar seleccionado no cambia.", "Masque les autres racines d'avatar. Votre avatar sélectionné n'est pas modifié.");
            Add("btn.clear_blueprint", "Clear blueprint ID (new upload)", "مسح blueprint ID (رفع جديد)", "Borrar blueprint ID (nueva subida)", "Effacer le blueprint ID (nouvel upload)");
            Add("tip.clear_blueprint", "Clears PipelineManager blueprint ID so the next upload creates a new avatar listing.", "يمسح blueprint ID من PipelineManager لإنشاء رفع جديد.", "Borra el blueprint ID de PipelineManager para una subida nueva.", "Efface le blueprint ID de PipelineManager pour qu'un nouvel upload crée une nouvelle fiche avatar.");
            Add("sec.tex_size", "Texture size", "حجم الأنسجة", "Tamaño de texturas", "Taille des textures");
            Add("stat.textures", "Textures", "الأنسجة", "Texturas", "Textures");
            Add("stat.mem_short", "Memory", "الذاكرة", "Memoria", "Mémoire");
            Add("field.cap_to", "Cap to", "الحد الأقصى", "Limitar a", "Limiter à");
            Add("cap.vrchat_max", "2048 (VRChat max)", "2048 (حد VRChat الأقصى)", "2048 (máx. VRChat)", "2048 (max VRChat)");
            Add("btn.reduce_tex", "Reduce to {0}px", "تصغير إلى {0}px", "Reducir a {0}px", "Réduire à {0}px");
            Add("tip.reduce_tex", "Lowers texture import max size. Does not delete texture assets.", "يخفض الحد الأقصى لحجم استيراد الأنسجة. لا يحذف ملفات الأنسجة.", "Reduce el tamaño máximo de importación. No elimina los assets de textura.", "Baisse la taille max d'import des textures. Ne supprime pas les assets de texture.");
            Add("cap.reduce_tex", "Changes import size only. Original files stay; use Restore to undo.", "يغيّر حجم الاستيراد فقط. الملفات الأصلية تبقى؛ استخدم الاستعادة للتراجع.", "Solo cambia el tamaño de importación. Los originales permanecen; usa Restaurar para deshacer.", "Change seulement la taille d'import. Les fichiers d'origine restent ; utilisez Restaurer pour annuler.");
            Add("btn.restore_tex", "Restore original sizes", "استعادة الأحجام الأصلية", "Restaurar tamaños originales", "Restaurer les tailles d'origine");
            Add("tip.restore_tex", "Restores texture import size to the source file resolution.", "يعيد حجم استيراد الأنسجة إلى دقة الملف الأصلي.", "Restaura el tamaño de importación a la resolución del archivo fuente.", "Restaure la taille d'import des textures à la résolution du fichier source.");
            Add("btn.mipmaps", "Enable mipmaps", "تفعيل mipmaps", "Activar mipmaps", "Activer les mipmaps");
            Add("tip.mipmaps", "Turns on mipmaps in texture import settings for avatar textures.", "يفعّل mipmaps في إعدادات استيراد أنسجة الأفاتار.", "Activa mipmaps en los ajustes de importación de texturas del avatar.", "Active les mipmaps dans les réglages d'import des textures de l'avatar.");
            Add("sec.quest", "Quest / Android", "Quest / Android", "Quest / Android", "Quest / Android", true);
            Add("quest.intro", "Quest uploads need VRChat/Mobile shaders. Materials are duplicated and colors/textures are copied so PC versions stay intact.", "رفع Quest يحتاج شيدرات VRChat/Mobile. تُنسخ المواد مع الألوان والأنسجة للإبقاء على نسخ PC.", "Las subidas Quest necesitan shaders VRChat/Mobile. Se duplican materiales copiando colores/texturas para conservar PC.", "Les uploads Quest nécessitent des shaders VRChat/Mobile. Les matériaux sont dupliqués et couleurs/textures copiés pour garder les versions PC intactes.");
            Add("stat.non_quest", "Non-Quest materials", "مواد غير متوافقة مع Quest", "Materiales no Quest", "Matériaux non Quest");
            Add("btn.quest_convert", "Convert to Quest shaders", "تحويل إلى شيدرات Quest", "Convertir a shaders Quest", "Convertir en shaders Quest");
            Add("tip.quest_convert", "Duplicates materials, prefers Toon Standard/Toon Lit, and copies main texture + color so Quest colors stay closer to PC.", "ينسخ المواد، يفضّل Toon Standard/Toon Lit، وينسخ النسيج الرئيسي واللون لتقارب ألوان Quest من PC.", "Duplica materiales, prefiere Toon Standard/Toon Lit y copia textura/color principal para acercar Quest a PC.", "Duplique les matériaux, préfère Toon Standard/Toon Lit, et copie texture + couleur principales pour rapprocher Quest du PC.");
            Add("cap.quest_convert", "Duplicates materials and transfers texture/color. Quest still looks flatter than PC shaders.", "ينسخ المواد وينقل النسيج واللون. مظهر Quest يبقى أبسط من شيدرات PC.", "Duplica materiales y transfiere textura/color. Quest sigue viéndose más plano que en PC.", "Duplique les matériaux et transfère texture/couleur. Quest reste plus plat que les shaders PC.");
            Add("dlg.ok", "OK", "حسناً", "OK", "OK");
            Add("dlg.cancel", "Cancel", "إلغاء", "Cancelar", "Annuler");
            Add("dlg.continue", "Continue", "متابعة", "Continuar", "Continuer");
            Add("dlg.done", "Done", "تم", "Listo", "Terminé");
            Add("dlg.fix", "Fix", "إصلاح", "Reparar", "Réparer");
            Add("dlg.reduce", "Reduce", "تقليل", "Reducir", "Réduire");
            Add("dlg.remove", "Remove", "إزالة", "Quitar", "Retirer");
            Add("dlg.disable", "Disable", "تعطيل", "Desactivar", "Désactiver");
            Add("dlg.clear", "Clear", "مسح", "Borrar", "Effacer");
            Add("dlg.rollback", "Rollback", "تراجع", "Revertir", "Restaurer");
            Add("dlg.restore", "Restore", "استعادة", "Restaurar", "Restaurer");
            Add("dlg.enable", "Enable", "تفعيل", "Activar", "Activer");
            Add("dlg.convert", "Convert", "تحويل", "Convertir", "Convertir");
            Add("dlg.fix_all.title", "Fix All", "إصلاح الكل", "Reparar todo", "Réparer tout");
            Add("dlg.fix_all.body", "Applies safe fixes only (materials, PipelineManager, audio, view, lip sync).\n" +
                "\n" +
                "Prefer Individual fixes when possible.\n" +
                "A rollback copy is saved first.\n" +
                "\n" +
                "Bounds are not changed by Fix All (use Individual if needed).\n" +
                "Head/face/hair meshes are not deleted.\n" +
                "\n" +
                "Continue?", "يطبق إصلاحات آمنة فقط (المواد، PipelineManager، الصوت، الرؤية، lip sync).\n" +
                "\n" +
                "فضّل الإصلاحات الفردية عند الإمكان.\n" +
                "تُحفظ نقطة تراجع أولاً.\n" +
                "\n" +
                "لا يغيّر Fix All الحدود (استخدم الفردي عند الحاجة).\n" +
                "لا تُحذف شبكات الرأس/الوجه/الشعر.\n" +
                "\n" +
                "متابعة؟", "Aplica solo reparaciones seguras (materiales, PipelineManager, audio, vista, lip sync).\n" +
                "\n" +
                "Prefiere reparaciones individuales cuando puedas.\n" +
                "Se guarda rollback primero.\n" +
                "\n" +
                "Fix All no cambia bounds (usa Individual si hace falta).\n" +
                "No se borran mallas de cabeza/cara/pelo.\n" +
                "\n" +
                "¿Continuar?", "Applique uniquement des réparations sûres (matériaux, PipelineManager, audio, vue, lip sync).\n" +
                "\n" +
                "Préférez les réparations individuelles si possible.\n" +
                "Une copie de restauration est enregistrée d'abord.\n" +
                "\n" +
                "Réparer tout ne change pas les bounds (utilisez Individuel si besoin).\n" +
                "Les maillages tête/visage/cheveux ne sont pas supprimés.\n" +
                "\n" +
                "Continuer ?");
            Add("dlg.fix_all.result", "Material slots fixed: {0}\n" +
                "PipelineManager added: {1}\n" +
                "Audio fixed: {2} (playOnAwake: {3})\n" +
                "View position: {4}\n" +
                "Lip sync: {5}\n" +
                "\n" +
                "Bounds were not changed (Individual only).\n" +
                "Re-check the Check tab. Fix pink/broken shaders manually.", "خانات المواد المصلحة: {0}\n" +
                "PipelineManager المضاف: {1}\n" +
                "الصوت المصلح: {2} (playOnAwake: {3})\n" +
                "موضع الرؤية: {4}\n" +
                "Lip sync: {5}\n" +
                "\n" +
                "لم تُغيَّر الحدود (فردي فقط).\n" +
                "أعد فحص تبويب Check. أصلح الشيدرات الوردية يدوياً.", "Ranuras de material reparadas: {0}\n" +
                "PipelineManager añadido: {1}\n" +
                "Audio reparado: {2} (playOnAwake: {3})\n" +
                "Posición de vista: {4}\n" +
                "Lip sync: {5}\n" +
                "\n" +
                "Bounds no se cambiaron (solo Individual).\n" +
                "Revisa la pestaña Check. Repara shaders rotos/rosas manualmente.", "Emplacements matériaux réparés : {0}\n" +
                "PipelineManager ajouté : {1}\n" +
                "Audio réparé : {2} (playOnAwake : {3})\n" +
                "Position de vue : {4}\n" +
                "Lip sync : {5}\n" +
                "\n" +
                "Les bounds n'ont pas été modifiés (Individuel seulement).\n" +
                "Revérifiez l'onglet Vérifier. Réparez les shaders roses/cassés manuellement.");
            Add("dlg.yes", "yes", "نعم", "sí", "oui");
            Add("dlg.no", "no", "لا", "no", "non");
            Add("dlg.set", "set", "مضبوط", "configurado", "défini");
            Add("dlg.skipped", "skipped", "تم التخطي", "omitido", "ignoré");
            Add("dlg.fix_complete", "Fix Complete", "اكتمل الإصلاح", "Reparación completa", "Réparation terminée");
            Add("dlg.pb.title", "PhysBones", "PhysBones", "PhysBones", "PhysBones", true);
            Add("dlg.pb.ok_body", "This avatar has {0} PhysBone component(s), which is within the 256 limit.", "هذا الأفاتار لديه {0} مكوّن PhysBone، وهو ضمن حد 256.", "Este avatar tiene {0} componente(s) PhysBone, dentro del límite de 256.", "Cet avatar a {0} composant(s) PhysBone, dans la limite de 256.");
            Add("dlg.pb.reduce_title", "Reduce PhysBones", "تقليل PhysBones", "Reducir PhysBones", "Réduire les PhysBones");
            Add("dlg.pb.reduce_body", "VRChat blocks upload above 256 PhysBone components.\n" +
                "\n" +
                "Current: {0}\n" +
                "Will remove: up to {1} PhysBone script(s)\n" +
                "Will keep: at least head/face/hair PhysBones\n" +
                "\n" +
                "SAFETY:\n" +
                "• Does NOT delete GameObjects, bones, meshes, or the head\n" +
                "• Never removes anything under Head / Face / Hair\n" +
                "• Only removes excess VRCPhysBone components elsewhere\n" +
                "• A rollback copy is saved first\n" +
                "\n" +
                "Continue?", "VRChat يمنع الرفع فوق 256 مكوّن PhysBone.\n" +
                "\n" +
                "الحالي: {0}\n" +
                "سيُزال: حتى {1} سكربت PhysBone\n" +
                "سيُبقى: على الأقل PhysBones الرأس/الوجه/الشعر\n" +
                "\n" +
                "الأمان:\n" +
                "• لا يحذف GameObjects أو العظام أو الشبكات أو الرأس\n" +
                "• لا يزيل أي شيء تحت Head / Face / Hair\n" +
                "• يزيل فقط مكوّنات VRCPhysBone الزائدة في أماكن أخرى\n" +
                "• تُحفظ نسخة تراجع أولاً\n" +
                "\n" +
                "متابعة؟", "VRChat bloquea la subida por encima de 256 componentes PhysBone.\n" +
                "\n" +
                "Actual: {0}\n" +
                "Se quitarán: hasta {1} script(s) PhysBone\n" +
                "Se conservarán: al menos PhysBones de cabeza/cara/pelo\n" +
                "\n" +
                "SEGURIDAD:\n" +
                "• NO elimina GameObjects, huesos, mallas ni la cabeza\n" +
                "• Nunca quita nada bajo Head / Face / Hair\n" +
                "• Solo quita componentes VRCPhysBone de más en otros sitios\n" +
                "• Se guarda un rollback primero\n" +
                "\n" +
                "¿Continuar?", "VRChat bloque l'upload au-dessus de 256 composants PhysBone.\n" +
                "\n" +
                "Actuel : {0}\n" +
                "À retirer : jusqu'à {1} script(s) PhysBone\n" +
                "À conserver : au moins les PhysBones tête/visage/cheveux\n" +
                "\n" +
                "SÉCURITÉ :\n" +
                "• Ne supprime PAS GameObjects, os, maillages ni la tête\n" +
                "• Ne retire jamais rien sous Head / Face / Hair\n" +
                "• Retire seulement les composants VRCPhysBone en trop ailleurs\n" +
                "• Une copie de restauration est enregistrée d'abord\n" +
                "\n" +
                "Continuer ?");
            Add("dlg.pb.done", "Removed {0} PhysBone component(s).\nBones, meshes, and head are unchanged.\nUse Rollback Avatar if you need to undo.", "أُزيل {0} مكوّن PhysBone.\nالعظام والشبكات والرأس لم تتغير.\nاستخدم تراجع الأفاتار إن أردت التراجع.", "Se quitaron {0} componente(s) PhysBone.\n" +
                "Huesos, mallas y cabeza sin cambios.\n" +
                "Usa Revertir avatar si necesitas deshacer.", "{0} composant(s) PhysBone retiré(s).\nOs, maillages et tête inchangés.\nUtilisez Restaurer l'avatar si besoin d'annuler.");
            Add("dlg.pb.head_kept", "Still {0} PhysBones because head/face/hair PhysBones are protected and were not removed. Reduce body/clothing PhysBones manually if needed.", "ما زال هناك {0} PhysBones لأن PhysBones الرأس/الوجه/الشعر محمية ولم تُزل. قلّل PhysBones الجسم/الملابس يدوياً إن لزم.", "Quedan {0} PhysBones porque los de cabeza/cara/pelo están protegidos y no se quitaron. Reduce PhysBones del cuerpo/ropa a mano si hace falta.", "Il reste {0} PhysBones car ceux de tête/visage/cheveux sont protégés et n'ont pas été retirés. Réduisez manuellement les PhysBones du corps/vêtements si besoin.");
            Add("dlg.pb.none_safe", "No safe PhysBones could be removed without touching head/face/hair. Nothing was deleted.", "لا يمكن إزالة PhysBones بأمان دون لمس الرأس/الوجه/الشعر. لم يُحذف شيء.", "No se pudieron quitar PhysBones sin tocar cabeza/cara/pelo. No se borró nada.", "Aucun PhysBone sûr à retirer sans toucher tête/visage/cheveux. Rien n'a été supprimé.");
            Add("dlg.missing.title", "Remove Missing Scripts", "إزالة السكربتات المفقودة", "Quitar scripts faltantes", "Retirer les scripts manquants");
            Add("dlg.missing.body", "This removes broken empty script slots from GameObjects.\n" +
                "\n" +
                "It does NOT delete meshes or child objects.\n" +
                "It never touches the head, face, or hair.\n" +
                "Only use if you know those scripts are gone for good.\n" +
                "\n" +
                "Continue?", "يزيل خانات السكربت الفارغة المعطلة من الكائنات.\n" +
                "\n" +
                "لا يحذف الشبكات أو الكائنات الفرعية.\n" +
                "لا يمس الرأس أو الوجه أو الشعر أبداً.\n" +
                "استخدمه فقط إن كنت متأكداً أن السكربتات ذهبت نهائياً.\n" +
                "\n" +
                "متابعة؟", "Quita ranuras de scripts rotas de los GameObjects.\n" +
                "\n" +
                "NO elimina mallas ni objetos hijos.\n" +
                "Nunca toca cabeza, cara ni pelo.\n" +
                "Úsalo solo si esos scripts ya no existen.\n" +
                "\n" +
                "¿Continuar?", "Retire les emplacements de scripts cassés des GameObjects.\n" +
                "\n" +
                "Ne supprime PAS maillages ni objets enfants.\n" +
                "Ne touche jamais tête, visage ou cheveux.\n" +
                "À utiliser seulement si ces scripts sont définitivement partis.\n" +
                "\n" +
                "Continuer ?");
            Add("dlg.missing.done", "Removed {0} missing script slot(s).", "أُزيلت {0} خانة سكربت مفقودة.", "Se quitaron {0} ranura(s) de scripts faltantes.", "{0} emplacement(s) de script manquant retiré(s).");
            Add("dlg.placeholder.title", "Placeholder Materials", "مواد مؤقتة", "Materiales marcador", "Matériaux placeholder");
            Add("dlg.placeholder.body", "Fills empty material slots with a gray placeholder.\n" +
                "\n" +
                "This can change how parts look. Prefer fixing materials manually.\n" +
                "\n" +
                "Continue?", "يملأ خانات المواد الفارغة بمادة رمادية مؤقتة.\n\nقد يغيّر المظهر. يُفضّل الإصلاح اليدوي.\n\nمتابعة؟", "Rellena ranuras vacías con un material gris.\n\nPuede cambiar el aspecto. Preferible reparar a mano.\n\n¿Continuar?", "Remplit les emplacements matériaux vides avec un placeholder gris.\n" +
                "\n" +
                "Cela peut changer l'apparence. Préférez réparer les matériaux à la main.\n" +
                "\n" +
                "Continuer ?");
            Add("dlg.placeholder.done", "Filled {0} slot(s).", "مُلئت {0} خانة.", "Se rellenaron {0} ranura(s).", "{0} emplacement(s) rempli(s).");
            Add("dlg.disable.title", "Disable Other Avatars", "تعطيل الأفاتارات الأخرى", "Desactivar otros avatares", "Désactiver les autres avatars");
            Add("dlg.disable.body", "Hides other avatar roots in this scene.\n\nYour selected avatar is not changed.\n\nContinue?", "يخفي جذور الأفاتارات الأخرى في هذا المشهد.\n\nالأفاتار المحدد لا يتغير.\n\nمتابعة؟", "Oculta otras raíces de avatar en esta escena.\n\nTu avatar seleccionado no cambia.\n\n¿Continuar?", "Masque les autres racines d'avatar dans cette scène.\n\nVotre avatar sélectionné n'est pas modifié.\n\nContinuer ?");
            Add("dlg.disable.done", "Disabled {0} other avatar(s).", "عُطّل {0} أفاتار آخر.", "Se desactivaron {0} avatar(es) más.", "{0} autre(s) avatar(s) désactivé(s).");
            Add("dlg.blueprint.title", "Clear Blueprint ID", "مسح Blueprint ID", "Borrar Blueprint ID", "Effacer le Blueprint ID");
            Add("dlg.blueprint.body", "Clears the PipelineManager blueprint ID for a fresh upload.\n\nContinue?", "يمسح blueprint ID من PipelineManager لرفع جديد.\n\nمتابعة؟", "Borra el blueprint ID de PipelineManager para una subida nueva.\n\n¿Continuar?", "Efface le blueprint ID de PipelineManager pour un upload neuf.\n\nContinuer ?");
            Add("dlg.blueprint.cleared", "Blueprint ID cleared.", "تم مسح Blueprint ID.", "Blueprint ID borrado.", "Blueprint ID effacé.");
            Add("dlg.blueprint.nothing", "Nothing to clear.", "لا شيء للمسح.", "Nada que borrar.", "Rien à effacer.");
            Add("dlg.rollback.title", "Rollback Avatar", "تراجع الأفاتار", "Revertir avatar", "Restaurer l'avatar");
            Add("dlg.rollback.body", "This replaces your avatar with the copy saved before Vtool changes.\n" +
                "\n" +
                "Texture import settings are restored too if they were changed.\n" +
                "\n" +
                "Continue?", "يستبدل أفاتارك بالنسخة المحفوظة قبل تغييرات Vtool.\n\nتُستعاد إعدادات استيراد الأنسجة أيضاً إن تغيّرت.\n\nمتابعة؟", "Reemplaza tu avatar con la copia guardada antes de los cambios de Vtool.\n" +
                "\n" +
                "También restaura importación de texturas si cambiaron.\n" +
                "\n" +
                "¿Continuar?", "Remplace votre avatar par la copie enregistrée avant les changements Vtool.\n" +
                "\n" +
                "Les réglages d'import des textures sont aussi restaurés s'ils ont changé.\n" +
                "\n" +
                "Continuer ?");
            Add("dlg.rollback.done_title", "Rollback Complete", "اكتمل التراجع", "Rollback completo", "Restauration terminée");
            Add("dlg.rollback.done", "Avatar restored.", "تمت استعادة الأفاتار.", "Avatar restaurado.", "Avatar restauré.");
            Add("dlg.backup.title", "Backup", "نسخ احتياطي", "Copia de seguridad", "Sauvegarde");
            Add("dlg.backup.done", "Created:\n{0}", "تم الإنشاء:\n{0}", "Creado:\n{0}", "Créé :\n{0}");
            Add("dlg.tex.reduce_title", "Reduce Textures", "تصغير الأنسجة", "Reducir texturas", "Réduire les textures");
            Add("dlg.tex.reduce_body", "Cap avatar textures to {0}px import size?", "تحديد حجم استيراد أنسجة الأفاتار إلى {0}px؟", "¿Limitar texturas del avatar a {0}px de importación?", "Limiter les textures de l'avatar à {0}px à l'import ?");
            Add("dlg.tex.reduce_done", "Reduced {0} texture(s). Use Restore to undo.", "صُغّرت {0} نسيج(ة). استخدم الاستعادة للتراجع.", "Se redujeron {0} textura(s). Usa Restaurar para deshacer.", "{0} texture(s) réduite(s). Utilisez Restaurer pour annuler.");
            Add("dlg.tex.restore_title", "Restore", "استعادة", "Restaurar", "Restaurer");
            Add("dlg.tex.restore_body", "Restore textures to source file resolution?", "استعادة الأنسجة إلى دقة الملف الأصلي؟", "¿Restaurar texturas a la resolución del archivo fuente?", "Restaurer les textures à la résolution du fichier source ?");
            Add("dlg.tex.restore_done", "Restored {0} texture(s).", "استُعيدت {0} نسيج(ة).", "Se restauraron {0} textura(s).", "{0} texture(s) restaurée(s).");
            Add("dlg.tex.mip_title", "Enable Mipmaps", "تفعيل Mipmaps", "Activar mipmaps", "Activer les mipmaps");
            Add("dlg.tex.mip_body", "Changes texture import settings for textures on this avatar. Continue?", "يغيّر إعدادات استيراد أنسجة هذا الأفاتار. متابعة؟", "Cambia los ajustes de importación de texturas de este avatar. ¿Continuar?", "Modifie les réglages d'import des textures de cet avatar. Continuer ?");
            Add("dlg.quest.title", "Quest Conversion", "تحويل Quest", "Conversión Quest", "Conversion Quest");
            Add("dlg.quest.body", "Duplicate materials, convert to VRChat Mobile shaders, and copy textures/colors for better Quest look?", "نسخ المواد، التحويل إلى شيدرات VRChat Mobile، ونسخ الأنسجة/الألوان لمظهر Quest أفضل؟", "¿Duplicar materiales, convertir a shaders VRChat Mobile y copiar texturas/colores para mejor aspecto Quest?", "Dupliquer les matériaux, convertir en shaders VRChat Mobile, et copier textures/couleurs pour un meilleur rendu Quest ?");
            Add("dlg.quest.done", "Converted {0} material slot(s).", "حُوّلت {0} خانة مادة.", "Se convirtieron {0} ranura(s) de material.", "{0} emplacement(s) de matériau converti(s).");
            Add("issue.no_descriptor", "Missing VRCAvatarDescriptor on avatar root", "VRCAvatarDescriptor مفقود على جذر الأفاتار", "Falta VRCAvatarDescriptor en la raíz del avatar", "VRCAvatarDescriptor manquant sur la racine de l'avatar");
            Add("hint.no_descriptor", "Add from VRChat SDK menu", "أضفه من قائمة VRChat SDK", "Añádelo desde el menú VRChat SDK", "Ajoutez-le depuis le menu VRChat SDK");
            Add("issue.no_pipeline", "Missing PipelineManager on avatar root", "PipelineManager مفقود على جذر الأفاتار", "Falta PipelineManager en la raíz del avatar", "PipelineManager manquant sur la racine de l'avatar");
            Add("hint.no_pipeline", "Use Fix All or add via SDK", "استخدم الإصلاح الشامل أو أضفه عبر SDK", "Usa Reparar todo o añádelo vía SDK", "Utilisez Réparer tout ou ajoutez-le via le SDK");
            Add("issue.no_humanoid", "Missing humanoid Animator on avatar root", "Animator من نوع Humanoid مفقود على جذر الأفاتار", "Falta Animator Humanoid en la raíz del avatar", "Animator Humanoid manquant sur la racine de l'avatar");
            Add("hint.no_humanoid", "Set rig to Humanoid in Import settings", "اضبط الـ rig إلى Humanoid في إعدادات الاستيراد", "Pon el rig en Humanoid en Import settings", "Réglez le rig sur Humanoid dans les réglages d'Import");
            Add("issue.missing_scripts", "{0} missing script reference(s)", "{0} مرجع سكربت مفقود", "{0} referencia(s) de script faltante(s)", "{0} référence(s) de script manquante(s)");
            Add("hint.missing_scripts", "Use Individual fixes (removes broken slots only)", "استخدم الإصلاحات الفردية (يزيل الخانات المعطلة فقط)", "Usa reparaciones individuales (solo quita ranuras rotas)", "Utilisez les réparations individuelles (retire seulement les emplacements cassés)");
            Add("issue.null_mats", "{0} null material slot(s)", "{0} خانة مادة فارغة", "{0} ranura(s) de material nula(s)", "{0} emplacement(s) matériau nul(s)");
            Add("hint.null_mats", "Fix All copies a nearby material on the same renderer", "الإصلاح الشامل ينسخ مادة قريبة على نفس العارض", "Reparar todo copia un material cercano del mismo renderer", "Réparer tout copie un matériau proche du même renderer");
            Add("issue.broken_shaders", "{0} broken shader(s) (pink materials)", "{0} شيدر معطل (مواد وردية)", "{0} shader(s) roto(s) (materiales rosas)", "{0} shader(s) cassé(s) (matériaux roses)");
            Add("hint.broken_shaders", "Reassign shaders manually", "أعد تعيين الشيدرات يدوياً", "Reasigna shaders manualmente", "Réassignez les shaders manuellement");
            Add("issue.missing_meshes", "{0} renderer(s) with missing mesh", "{0} عارض بشبكة مفقودة", "{0} renderer(s) con malla faltante", "{0} renderer(s) avec maillage manquant");
            Add("hint.missing_meshes", "Reassign or remove broken renderers", "أعد التعيين أو أزل العوارض المعطلة", "Reasigna o quita renderers rotos", "Réassignez ou retirez les renderers cassés");
            Add("issue.extreme_poly", "Extreme polygon count ({0})", "عدد مضلعات مرتفع جداً ({0})", "Conteo de polígonos extremo ({0})", "Nombre de polygones extrême ({0})");
            Add("hint.extreme_poly", "Reduce in Blender or decimate", "قلّل في Blender أو استخدم decimate", "Reduce en Blender o usa decimate", "Réduisez dans Blender ou utilisez decimate");
            Add("issue.physbone_limit", "Phys Bone Components: {0} — exceeds VRChat limit (256)", "مكوّنات Phys Bone: {0} — تتجاوز حد VRChat (256)", "Componentes Phys Bone: {0} — supera el límite de VRChat (256)", "Composants Phys Bone : {0} — dépasse la limite VRChat (256)");
            Add("hint.physbone_limit", "Use Individual fixes → Reduce PhysBones to 256 (scripts only; head/face/hair never touched)", "استخدم الإصلاحات الفردية → تقليل PhysBones إلى 256 (سكربتات فقط؛ الرأس/الوجه/الشعر لا تُمس)", "Usa reparaciones individuales → Reducir PhysBones a 256 (solo scripts; cabeza/cara/pelo intactos)", "Utilisez Réparations individuelles → Réduire PhysBones à 256 (scripts seulement ; tête/visage/cheveux intactes)");
            Add("issue.no_chest", "Humanoid rig missing Chest bone mapping", "هيكل Humanoid بلا تعيين عظمة Chest", "Rig Humanoid sin mapeo de hueso Chest", "Rig Humanoid sans mapping de l'os Chest");
            Add("hint.no_chest", "Map Chest in Rig configuration", "عيّن Chest في إعدادات الـ Rig", "Mapea Chest en la configuración del Rig", "Mappez Chest dans la configuration du Rig");
            Add("issue.no_view", "View position not set on descriptor", "موضع الرؤية غير مضبوط على الـ descriptor", "Posición de vista no configurada en el descriptor", "Position de vue non définie sur le descriptor");
            Add("hint.fix_if_empty", "Fix All sets it only when empty", "الإصلاح الشامل يضبطه فقط إن كان فارغاً", "Reparar todo lo configura solo si está vacío", "Réparer tout le définit seulement si vide");
            Add("issue.no_lipsync", "Lip sync / visemes not configured", "Lip sync / visemes غير مضبوط", "Lip sync / visemes no configurados", "Lip sync / visemes non configurés");
            Add("issue.root_scale", "Avatar root scale is not (1,1,1)", "مقياس جذر الأفاتار ليس (1,1,1)", "La escala de la raíz del avatar no es (1,1,1)", "L'échelle de la racine de l'avatar n'est pas (1,1,1)");
            Add("hint.root_scale", "Can cause IK issues — normalize if needed", "قد يسبب مشاكل IK — طبّع إن لزم", "Puede causar problemas de IK — normaliza si hace falta", "Peut causer des problèmes d'IK — normalisez si besoin");
            Add("issue.neg_scale", "{0} transform(s) with negative scale", "{0} تحويل بمقياس سالب", "{0} transform(s) con escala negativa", "{0} transform(s) avec échelle négative");
            Add("hint.neg_scale", "Can invert normals and break uploads", "قد يعكس النورملز ويعطل الرفع", "Puede invertir normales y romper la subida", "Peut inverser les normales et casser l'upload");
            Add("issue.nonunit_scale", "{0} transform(s) with non-unit scale", "{0} تحويل بمقياس غير واحد", "{0} transform(s) con escala distinta de 1", "{0} transform(s) avec échelle différente de 1");
            Add("hint.nonunit_scale", "May cause animation/IK issues", "قد يسبب مشاكل حركة/IK", "Puede causar problemas de animación/IK", "Peut causer des problèmes d'animation/IK");
            Add("issue.high_poly", "High polygon count ({0}) — Poor rank on PC", "عدد مضلعات مرتفع ({0}) — رتبة Poor على PC", "Alto conteo de polígonos ({0}) — rango Poor en PC", "Nombre de polygones élevé ({0}) — rang Poor sur PC");
            Add("hint.high_poly", "Decimate or optimize mesh", "قلّل أو حسّن الشبكة", "Decima u optimiza la malla", "Decimatez ou optimisez le maillage");
            Add("issue.quest_poly", "Over Quest limit ({0} tris)", "فوق حد Quest ({0} مثلث)", "Sobre el límite Quest ({0} tris)", "Au-dessus de la limite Quest ({0} tris)");
            Add("hint.quest_poly", "Required for Android/Quest uploads", "مطلوب لرفع Android/Quest", "Requerido para subidas Android/Quest", "Requis pour les uploads Android/Quest");
            Add("issue.skinned_many", "{0} skinned meshes (8+ hurts performance)", "{0} شبكات جلدية (8+ يضر الأداء)", "{0} mallas skinned (8+ empeora el rendimiento)", "{0} maillages skinned (8+ nuit aux performances)");
            Add("hint.skinned_many", "Merge meshes if possible", "ادمج الشبكات إن أمكن", "Fusiona mallas si es posible", "Fusionnez les maillages si possible");
            Add("issue.mats_many", "{0} material slots (16+ hurts performance)", "{0} خانة مادة (16+ يضر الأداء)", "{0} ranuras de material (16+ empeora el rendimiento)", "{0} emplacements matériaux (16+ nuit aux performances)");
            Add("hint.mats_many", "Atlas textures / merge materials", "اجمع الأنسجة / ادمج المواد", "Haz atlas / fusiona materiales", "Atlas de textures / fusionnez les matériaux");
            Add("issue.tex_4k", "{0} texture(s) at 4K+", "{0} نسيج بدقة 4K+", "{0} textura(s) en 4K+", "{0} texture(s) en 4K+");
            Add("hint.tex_4k", "Reduce to 2K in Textures tab", "قلّل إلى 2K في تبويب Textures", "Reduce a 2K en la pestaña Textures", "Réduisez à 2K dans l'onglet Textures");
            Add("issue.tex_2k", "{0} texture(s) over 2K", "{0} نسيج فوق 2K", "{0} textura(s) sobre 2K", "{0} texture(s) au-dessus de 2K");
            Add("hint.tex_2k", "VRChat recommends 2K max", "VRChat يوصي بحد أقصى 2K", "VRChat recomienda 2K máximo", "VRChat recommande 2K maximum");
            Add("issue.tex_mem", "High texture memory (~{0} MB)", "ذاكرة أنسجة مرتفعة (~{0} MB)", "Alta memoria de texturas (~{0} MB)", "Mémoire textures élevée (~{0} MB)");
            Add("hint.tex_mem", "Can fail security checks", "قد يفشل فحوصات الأمان", "Puede fallar comprobaciones de seguridad", "Peut faire échouer les contrôles de sécurité");
            Add("issue.no_mip", "{0} texture(s) missing mipmaps", "{0} نسيج بدون mipmaps", "{0} textura(s) sin mipmaps", "{0} texture(s) sans mipmaps");
            Add("hint.use_tex_tab", "Use Textures tab", "استخدم تبويب Textures", "Usa la pestaña Textures", "Utilisez l'onglet Textures");
            Add("issue.dynbone", "{0} legacy Dynamic Bone(s)", "{0} Dynamic Bone قديم", "{0} Dynamic Bone(s) legado(s)", "{0} Dynamic Bone(s) hérités");
            Add("hint.dynbone", "Migrate to PhysBones", "انقل إلى PhysBones", "Migra a PhysBones", "Migrez vers PhysBones");
            Add("issue.pb_poor", "{0} PhysBones (32+ is Very Poor on PC)", "{0} PhysBones (32+ رتبة Very Poor على PC)", "{0} PhysBones (32+ es Very Poor en PC)", "{0} PhysBones (32+ = Very Poor sur PC)");
            Add("hint.pb_poor", "Consider combining or reducing PhysBones", "فكّر بدمج أو تقليل PhysBones", "Considera combinar o reducir PhysBones", "Envisagez de combiner ou réduire les PhysBones");
            Add("issue.pb_quest", "{0} PhysBones — Quest/mobile hard cap is 8 components", "{0} PhysBones — حد Quest/الجوال الصارم هو 8 مكوّنات", "{0} PhysBones — el límite duro de Quest/móvil es 8 componentes", "{0} PhysBones — la limite stricte Quest/mobile est de 8 composants");
            Add("hint.pb_quest", "For Quest uploads keep at most 8 PhysBone components", "لرفع Quest أبقِ 8 مكوّنات PhysBone كحد أقصى", "Para Quest mantén como máximo 8 componentes PhysBone", "Pour Quest, gardez au plus 8 composants PhysBone");
            Add("issue.pb_quest_xf", "{0} PhysBone transforms — Quest/mobile hard cap is 64", "{0} تحويلات PhysBone — حد Quest/الجوال هو 64", "{0} transforms PhysBone — el límite duro de Quest/móvil es 64", "{0} transforms PhysBone — limite stricte Quest/mobile = 64");
            Add("hint.pb_quest_xf", "Split or shorten PhysBone chains for Quest", "قسّم أو قصّر سلاسل PhysBone لـ Quest", "Divide o acorta cadenas PhysBone para Quest", "Divisez ou raccourcissez les chaînes PhysBone pour Quest");
            Add("issue.pb_xf_limit", "{0} PhysBone(s) affect more than 256 transforms each (VRChat per-component limit)", "{0} PhysBone يؤثر على أكثر من 256 تحويلاً لكل مكوّن (حد VRChat)", "{0} PhysBone(s) afectan más de 256 transforms cada uno (límite por componente)", "{0} PhysBone(s) affectent plus de 256 transforms chacun (limite VRChat par composant)");
            Add("hint.pb_xf_limit", "Split that PhysBone chain — one component cannot drive more than 256 transforms", "قسّم سلسلة PhysBone — المكوّن الواحد لا يمكنه تحريك أكثر من 256 تحويلاً", "Divide esa cadena PhysBone — un componente no puede mover más de 256 transforms", "Divisez cette chaîne PhysBone — un composant ne peut pas entraîner plus de 256 transforms");
            Add("issue.pb_colliders", "{0} PhysBone colliders (16+ is over Quest/mobile Poor)", "{0} PhysBone collider (16+ يتجاوز Quest/الجوال)", "{0} colliders PhysBone (16+ supera Quest/móvil)", "{0} colliders PhysBone (16+ dépasse Quest/mobile Poor)");
            Add("hint.pb_colliders", "Reduce PhysBone colliders for Quest", "قلّل PhysBone colliders لـ Quest", "Reduce colliders PhysBone para Quest", "Réduisez les colliders PhysBone pour Quest");
            Add("stat.pb_xf", "PB transforms", "تحويلات PB", "Transforms PB", "Transforms PB");
            Add("stat.pb_colliders", "PB colliders", "PB colliders", "Colliders PB", "Colliders PB");
            Add("issue.bad_audio", "{0} audio source(s) need 3D spatialization", "{0} مصدر صوت يحتاج تموضعاً ثلاثي الأبعاد", "{0} fuente(s) de audio necesitan espacialización 3D", "{0} source(s) audio sans spatialisation 3D");
            Add("hint.fix_all_audio", "Fix All corrects audio", "الإصلاح الشامل يصحح الصوت", "Reparar todo corrige el audio", "Réparer tout corrige l'audio");
            Add("issue.play_awake", "{0} audio plays on awake", "{0} صوت يعمل عند التشغيل", "{0} audio se reproduce al activar", "{0} audio joue au démarrage");
            Add("hint.play_awake", "Fix All disables playOnAwake", "الإصلاح الشامل يعطّل playOnAwake", "Reparar todo desactiva playOnAwake", "Réparer tout désactive playOnAwake");
            Add("issue.particles", "{0} particle systems (16+ hurts performance)", "{0} نظام جزيئات (16+ يضر الأداء)", "{0} sistemas de partículas (16+ empeora el rendimiento)", "{0} systèmes de particules (16+ nuit aux performances)");
            Add("hint.particles", "Reduce particle count", "قلّل عدد الجزيئات", "Reduce la cantidad de partículas", "Réduisez le nombre de particules");
            Add("issue.other_avatars", "{0} other avatar(s) active in scene", "{0} أفاتار آخر نشط في المشهد", "{0} otro(s) avatar(es) activo(s) en la escena", "{0} autre(s) avatar(s) actif(s) dans la scène");
            Add("hint.other_avatars", "Use Individual fixes to hide them", "استخدم الإصلاحات الفردية لإخفائها", "Usa reparaciones individuales para ocultarlos", "Utilisez les réparations individuelles pour les masquer");
            Add("issue.quest_mats", "{0} material(s) not Quest-compatible", "{0} مادة غير متوافقة مع Quest", "{0} material(es) no compatible(s) con Quest", "{0} matériau(x) non compatible(s) Quest");
            Add("hint.quest_mats", "Use Quest conversion in Textures tab", "استخدم تحويل Quest في تبويب Textures", "Usa la conversión Quest en la pestaña Textures", "Utilisez la conversion Quest dans l'onglet Textures");
            Add("issue.height", "Unusual avatar height ({0}m)", "ارتفاع أفاتار غير معتاد ({0}m)", "Altura de avatar inusual ({0}m)", "Hauteur d'avatar inhabituelle ({0}m)");
            Add("hint.height", "Check view position and scale", "تحقق من موضع الرؤية والمقياس", "Revisa posición de vista y escala", "Vérifiez la position de vue et l'échelle");
        }
    }
}
