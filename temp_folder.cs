#nullable disable

#pragma warning disable CA1002, CA1721, CA1819, CS1591

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using J2N.Collections.Generic.Extensions;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Extensions;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LibraryTaskScheduler;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;
using MusicAlbum = MediaBrowser.Controller.Entities.Audio.MusicAlbum;
using Season = MediaBrowser.Controller.Entities.TV.Season;
using Series = MediaBrowser.Controller.Entities.TV.Series;

namespace MediaBrowser.Controller.Entities
{
    /// <summary>
    /// Class Folder.
    /// </summary>
    public class Folder : BaseItem
    {
        private IEnumerable<BaseItem> _children;
        private LinkedChild[] _linkedChildren = [];

        public static IUserViewManager UserViewManager { get; set; }

        public static ILimitedConcurrencyLibraryScheduler LimitedConcurrencyLibraryScheduler { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is root.
        /// </summary>
        /// <value><c>true</c> if this instance is root; otherwise, <c>false</c>.</value>
        public bool IsRoot { get; set; }

        /// <summary>
        /// Gets or sets the linked children.
        /// </summary>
        [JsonIgnore]
        public LinkedChild[] LinkedChildren
        {
            get => _linkedChildren;
            set
            {
                _linkedChildren = value;

                // Assigning the collection means the caller knows the complete set of links.
                LinkedChildrenLoaded = true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether <see cref="LinkedChildren"/> holds the stored set of links.
        /// </summary>
        /// <remarks>
        /// An unloaded instance carries an empty array that means "unknown", not "no children" 鈥?        /// persisting it would delete every link the item has.
        /// </remarks>
        [JsonIgnore]
        public bool LinkedChildrenLoaded { get; private set; }

        [JsonIgnore]
        public DateTime? DateLastMediaAdded { get; set; }

        [JsonIgnore]
        public override bool SupportsThemeMedia => true;

        [JsonIgnore]
        public virtual bool IsPreSorted => false;

        [JsonIgnore]
        public virtual bool IsPhysicalRoot => false;

        [JsonIgnore]
        public override bool SupportsInheritedParentImages => true;

        [JsonIgnore]
        public override bool SupportsPlayedStatus => true;

        /// <summary>
        /// Gets a value indicating whether this instance is folder.
        /// </summary>
        /// <value><c>true</c> if this instance is folder; otherwise, <c>false</c>.</value>
        [JsonIgnore]
        public override bool IsFolder => true;

        [JsonIgnore]
        public override bool IsDisplayedAsFolder => true;

        [JsonIgnore]
        public virtual bool SupportsCumulativeRunTimeTicks => false;

        [JsonIgnore]
        public virtual bool SupportsDateLastMediaAdded => false;

        [JsonIgnore]
        public override string FileNameWithoutExtension
        {
            get
            {
                if (IsFileProtocol)
                {
                    return System.IO.Path.GetFileName(Path);
                }

                return null;
            }
        }

        /// <summary>
        /// Gets or Sets the actual children.
        /// </summary>
        /// <value>The actual children.</value>
        [JsonIgnore]
        public virtual IEnumerable<BaseItem> Children
        {
            get => _children ??= LoadChildren();
            set => _children = value;
        }

        /// <summary>
        /// Gets thread-safe access to all recursive children of this folder - without regard to user.
        /// </summary>
        /// <value>The recursive children.</value>
        [JsonIgnore]
        public IEnumerable<BaseItem> RecursiveChildren => GetRecursiveChildren();

        [JsonIgnore]
        protected virtual bool SupportsShortcutChildren => false;

        protected virtual bool FilterLinkedChildrenPerUser => false;

        [JsonIgnore]
        protected override bool SupportsOwnedItems => base.SupportsOwnedItems || SupportsShortcutChildren;

        [JsonIgnore]
        public virtual bool SupportsUserDataFromChildren
        {
            get
            {
                // These are just far too slow.
                if (this is ICollectionFolder)
                {
                    return false;
                }

                if (this is UserView)
                {
                    return false;
                }

                if (this is UserRootFolder)
                {
                    return false;
                }

                if (this is Channel)
                {
                    return false;
                }

                if (SourceType != SourceType.Library)
                {
                    return false;
                }

                if (this is IItemByName)
                {
                    if (this is not IHasDualAccess hasDualAccess || hasDualAccess.IsAccessedByName)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public static ICollectionManager CollectionManager { get; set; }

        public override bool CanDelete()
        {
            if (IsRoot)
            {
                return false;
            }

            return base.CanDelete();
        }

        public override bool RequiresRefresh()
        {
            var baseResult = base.RequiresRefresh();

            if (SupportsCumulativeRunTimeTicks && !RunTimeTicks.HasValue)
            {
                baseResult = true;
            }

            return baseResult;
        }

        /// <summary>
        /// Adds the child.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="InvalidOperationException">Unable to add  + item.Name.</exception>
        public void AddChild(BaseItem item)
        {
            item.SetParent(this);

            if (item.Id.IsEmpty())
            {
                item.Id = LibraryManager.GetNewItemId(item.Path, item.GetType());
            }

            if (item.DateCreated == DateTime.MinValue)
            {
                item.DateCreated = DateTime.UtcNow;
            }

            if (item.DateModified == DateTime.MinValue)
            {
                item.DateModified = DateTime.UtcNow;
            }

            LibraryManager.CreateItem(item, this);
        }

        public override bool IsVisible(User user, bool skipAllowedTagsCheck = false)
        {
            if (this is ICollectionFolder && this is not BasePluginFolder)
            {
                var blockedMediaFolders = user.GetPreferenceValues<Guid>(PreferenceKind.BlockedMediaFolders);
                if (blockedMediaFolders.Length > 0)
                {
                    if (blockedMediaFolders.Contains(Id))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!user.HasPermission(PermissionKind.EnableAllFolders)
                        && !user.GetPreferenceValues<Guid>(PreferenceKind.EnabledFolders).Contains(Id))
                    {
                        return false;
                    }
                }
            }

            return base.IsVisible(user, skipAllowedTagsCheck);
        }

        /// <summary>
        /// Loads our children.  Validation will occur externally.
        /// We want this synchronous.
        /// </summary>
        /// <returns>Returns children.</returns>
        protected virtual IReadOnlyList<BaseItem> LoadChildren()
        {
            // logger.LogDebug("Loading children from {0} {1} {2}", GetType().Name, Id, Path);
            // just load our children from the repo - the library will be validated and maintained in other processes
            return GetCachedChildren();
        }

        public override double? GetRefreshProgress()
        {
            return ProviderManager.GetRefreshProgress(Id);
        }

        public Task ValidateChildren(IProgress<double> progress, CancellationToken cancellationToken)
        {
            return ValidateChildren(progress, new MetadataRefreshOptions(new DirectoryService(FileSystem)), cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Validates that the children of the folder still exist.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="metadataRefreshOptions">The metadata refresh options.</param>
        /// <param name="recursive">if set to <c>true</c> [recursive].</param>
        /// <param name="allowRemoveRoot">remove item even this folder is root.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task ValidateChildren(IProgress<double> progress, MetadataRefreshOptions metadataRefreshOptions, bool recursive = true, bool allowRemoveRoot = false, CancellationToken cancellationToken = default)
        {
            Children = null; // invalidate cached children.
            return ValidateChildrenInternal(progress, recursive, true, allowRemoveRoot, metadataRefreshOptions, metadataRefreshOptions.DirectoryService, cancellationToken);
        }

        private Dictionary<Guid, BaseItem> GetActualChildrenDictionary()
        {
            var dictionary = new Dictionary<Guid, BaseItem>();

            Children = null; // invalidate cached children.
            var childrenList = Children.ToList();

            foreach (var child in childrenList)
            {
                var id = child.Id;
                if (dictionary.ContainsKey(id))
                {
                    Logger.LogError(
                        "Found folder containing items with duplicate id. Path: {Path}, Child Name: {ChildName}",
                        Path ?? Name,
                        child.Path ?? child.Name);
                }
                else
                {
                    dictionary[id] = child;
                }
            }

            return dictionary;
        }

        /// <summary>
        /// Validates the children internal.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="recursive">if set to <c>true</c> [recursive].</param>
        /// <param name="refreshChildMetadata">if set to <c>true</c> [refresh child metadata].</param>
        /// <param name="allowRemoveRoot">remove item even this folder is root.</param>
        /// <param name="refreshOptions">The refresh options.</param>
        /// <param name="directoryService">The directory service.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        protected virtual async Task ValidateChildrenInternal(IProgress<double> progress, bool recursive, bool refreshChildMetadata, bool allowRemoveRoot, MetadataRefreshOptions refreshOptions, IDirectoryService directoryService, CancellationToken cancellationToken)
        {
            if (recursive)
            {
                ProviderManager.OnRefreshStart(this);
            }

            try
            {
                if (GetParents().Any(f => f.Id.Equals(Id)))
                {
                    throw new InvalidOperationException("Recursive datastructure detected abort processing this item.");
                }

                await ValidateChildrenInternal2(progress, recursive, refreshChildMetadata, allowRemoveRoot, refreshOptions, directoryService, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (recursive)
                {
                    ProviderManager.OnRefreshComplete(this);
                }
            }
        }

        private static bool IsLibraryFolderAccessible(IDirectoryService directoryService, BaseItem item, bool checkCollection)
        {
            if (!checkCollection && (item is BoxSet || string.Equals(item.FileNameWithoutExtension, "collections", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // For top parents i.e. Library folders, skip the validation if it's empty or inaccessible
            if (item.IsTopParent && !directoryService.IsAccessible(item.ContainingFolderPath))
            {
                Logger.LogWarning("Library folder {LibraryFolderPath} is inaccessible or empty, skipping", item.ContainingFolderPath);
                return false;
            }

            return true;
        }

        private async Task ValidateChildrenInternal2(IProgress<double> progress, bool recursive, bool refreshChildMetadata, bool allowRemoveRoot, MetadataRefreshOptions refreshOptions, IDirectoryService directoryService, CancellationToken cancellationToken)
        {
            if (!IsLibraryFolderAccessible(directoryService, this, allowRemoveRoot))
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var validChildren = new List<BaseItem>();
            var accessibleChildren = new List<BaseItem>();
            var validChildrenNeedGeneration = false;

            if (IsFileProtocol)
            {
                IEnumerable<BaseItem> nonCachedChildren = [];

                try
                {
                    nonCachedChildren = GetNonCachedChildren(directoryService);
                }
                catch (IOException ex)
                {
                    Logger.LogError(ex, "Error retrieving children from file system");
                }
                catch (SecurityException ex)
                {
                    Logger.LogError(ex, "Error retrieving children from file system");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error retrieving children");
                    return;
                }

                progress.Report(ProgressHelpers.RetrievedChildren);

                if (recursive)
                {
                    ProviderManager.OnRefreshProgress(this, ProgressHelpers.RetrievedChildren);
                }

                // Build a dictionary of the current children we have now by Id so we can compare quickly and easily
                var currentChildren = GetActualChildrenDictionary();

                // Create a list for our validated children
                var newItems = new List<BaseItem>();
                var actuallyRemoved = new List<BaseItem>();

                // Build a reverse path鈫抜tem lookup for detecting type changes
                var currentChildrenByPath = new Dictionary<string, BaseItem>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in currentChildren)
                {
                    if (!string.IsNullOrEmpty(kvp.Value.Path))
                    {
                        currentChildrenByPath.TryAdd(kvp.Value.Path, kvp.Value);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                foreach (var child in nonCachedChildren)
                {
                    if (!IsLibraryFolderAccessible(directoryService, child, allowRemoveRoot))
                    {
                        // Preserve inaccessible items so they aren't treated as removed.
                        if (currentChildren.TryGetValue(child.Id, out var childrenToKeep))
                        {
                            validChildren.Add(childrenToKeep);
                        }

                        continue;
                    }

                    if (currentChildren.TryGetValue(child.Id, out BaseItem currentChild))
                    {
                        validChildren.Add(currentChild);
                        accessibleChildren.Add(currentChild);

                        if (currentChild.UpdateFromResolvedItem(child) > ItemUpdateType.None)
                        {
                            await currentChild.UpdateToRepositoryAsync(ItemUpdateType.MetadataImport, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            // metadata is up-to-date; make sure DB has correct images dimensions and hash
                            await LibraryManager.UpdateImagesAsync(currentChild).ConfigureAwait(false);
                        }

                        continue;
                    }

                    // Check if an existing item occupies the same path with different type/ID
                    if (!string.IsNullOrEmpty(child.Path)
                        && currentChildrenByPath.TryGetValue(child.Path, out var staleItem)
                        && !staleItem.Id.Equals(child.Id))
                    {
                        Logger.LogInformation(
                            "Item type changed at {Path}: {OldType} -> {NewType}, removing stale entry",
                            child.Path,
                            staleItem.GetType().Name,
                            child.GetType().Name);

                        currentChildren.Remove(staleItem.Id);
                        currentChildrenByPath.Remove(child.Path);
                        staleItem.SetParent(null);
                        LibraryManager.DeleteItem(staleItem, new DeleteOptions { DeleteFileLocation = false }, this, false);
                        actuallyRemoved.Add(staleItem);
                    }

                    // Brand new item - needs to be added
                    child.SetParent(this);
                    newItems.Add(child);
                    validChildren.Add(child);
                    accessibleChildren.Add(child);
                }

                // That's all the new and changed ones - now see if any have been removed and need cleanup
                var itemsRemoved = currentChildren.Values.Except(validChildren).ToList();

                // If it's an AggregateFolder, don't remove
                // Collect replaced primaries for deferred deletion (after CreateItems)
                var replacedPrimaries = new List<(Video OldPrimary, Video NewPrimary)>();

                // Build a set of paths that are alternate versions of valid children
                // These items should not be deleted - they're managed by their primary video
                var alternateVersionPaths = validChildren
                    .OfType<Video>()
                    .SelectMany(v => v.LocalAlternateVersions ?? [])
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (itemsRemoved.Count > 0)
                {
                    foreach (var item in itemsRemoved)
                    {
                        if (!item.CanDelete())
                        {
                            Logger.LogDebug("Item marked as non-removable, skipping: {Path}", item.Path ?? item.Name);
                            continue;
                        }

                        // Skip items that are alternate versions of another video
                        if (item is Video video)
                        {
                            // Check if path is in LocalAlternateVersions of any valid child
                            if (!string.IsNullOrEmpty(item.Path) && alternateVersionPaths.Contains(item.Path))
                            {
                                Logger.LogDebug("Item path matches an alternate version, skipping deletion: {Path}", item.Path);
                                continue;
                            }
                        }

                        // Defer deletion if this primary video is being replaced by a new primary
                        // that takes over its alternates. Deleting now would trigger premature
                        // promotion inside DeleteItem and write stale paths to collection NFOs.
                        if (item is Video primaryVideo
                            && !primaryVideo.PrimaryVersionId.HasValue
                            && primaryVideo.OwnerId.IsEmpty()
                            && (primaryVideo.LocalAlternateVersions ?? []).Any(p => alternateVersionPaths.Contains(p)))
                        {
                            var newPrimary = newItems
                                .OfType<Video>()
                                .FirstOrDefault(v => (v.LocalAlternateVersions ?? [])
                                    .Any(p => (primaryVideo.LocalAlternateVersions ?? [])
                                        .Any(op => string.Equals(op, p, StringComparison.OrdinalIgnoreCase))));
                            if (newPrimary is not null)
                            {
                                Logger.LogDebug("Deferring deletion of replaced primary: {Path}", item.Path);
                                replacedPrimaries.Add((primaryVideo, newPrimary));
                                actuallyRemoved.Add(item);
                                item.SetParent(null);
                                continue;
                            }
                        }

                        if (item.IsFileProtocol)
                        {
                            Logger.LogDebug("Removed item: {Path}", item.Path);

                            actuallyRemoved.Add(item);
                            item.SetParent(null);
                            LibraryManager.DeleteItem(item, new DeleteOptions { DeleteFileLocation = false }, this, false);
                        }
                    }
                }

                if (newItems.Count > 0)
                {
                    LibraryManager.CreateItems(newItems, this, cancellationToken);
                }

                // Process deferred replaced-primary deletions now that new primaries exist in DB/cache.
                // This avoids the premature promotion that would occur if DeleteItem ran before CreateItems.
                foreach (var (oldPrimary, newPrimary) in replacedPrimaries)
                {
                    Logger.LogInformation(
                        "Processing deferred deletion of replaced primary {OldName} ({OldId}), new primary {NewName} ({NewId})",
                        oldPrimary.Name,
                        oldPrimary.Id,
                        newPrimary.Name,
                        newPrimary.Id);

                    // Reroute collection/playlist references from old primary to new primary
                    await LibraryManager.RerouteLinkedChildReferencesAsync(oldPrimary.Id, newPrimary.Id).ConfigureAwait(false);

                    // Transfer alternates from old primary to new primary
                    var localAlternateIds = LibraryManager.GetLocalAlternateVersionIds(oldPrimary).ToHashSet();
                    var allAlternateIds = localAlternateIds
                        .Concat(LibraryManager.GetLinkedAlternateVersions(oldPrimary).Select(v => v.Id))
                        .Distinct()
                        .ToList();

                    foreach (var altId in allAlternateIds)
                    {
                        if (LibraryManager.GetItemById(altId) is Video altVideo && !altVideo.Id.Equals(newPrimary.Id))
                        {
                            altVideo.SetPrimaryVersionId(newPrimary.Id);
                            altVideo.OwnerId = localAlternateIds.Contains(altVideo.Id) ? newPrimary.Id : Guid.Empty;
                            await altVideo.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    // Clear alternate arrays so DeleteItem won't trigger promotion
                    oldPrimary.LocalAlternateVersions = [];
                    oldPrimary.LinkedAlternateVersions = [];

                    // Safe to delete now 鈥?no promotion will happen
                    LibraryManager.DeleteItem(oldPrimary, new DeleteOptions { DeleteFileLocation = false }, this, false);
                }

                // Demote old primaries that are now alternate versions of newly created primaries.
                // This handles the case where a new file is added that becomes the new primary
                // (e.g. movie-2 added, movie-3 was primary 鈫?movie-3 needs demotion).
                // Items in replacedPrimaries are excluded (already in actuallyRemoved).
                var oldPrimariesToDemote = new List<(Video OldPrimary, Video NewPrimary)>();
                foreach (var item in itemsRemoved.Except(actuallyRemoved))
                {
                    if (item is Video video
                        && video.OwnerId.IsEmpty()
                        && !string.IsNullOrEmpty(item.Path)
                        && alternateVersionPaths.Contains(item.Path))
                    {
                        var newPrimary = newItems
                            .OfType<Video>()
                            .FirstOrDefault(v => (v.LocalAlternateVersions ?? [])
                                .Any(p => string.Equals(p, item.Path, StringComparison.OrdinalIgnoreCase)));
                        if (newPrimary is not null)
                        {
                            oldPrimariesToDemote.Add((video, newPrimary));
                        }
                    }
                }

                foreach (var (oldPrimary, newPrimary) in oldPrimariesToDemote)
                {
                    Logger.LogInformation(
                        "Demoting old primary {OldName} ({OldId}) to alternate of new primary {NewName} ({NewId})",
                        oldPrimary.Name,
                        oldPrimary.Id,
                        newPrimary.Name,
                        newPrimary.Id);

                    // First: update old primary's alternate items to point to new primary.
                    // Order matters 鈥?update alternates FIRST so they don't get orphan-deleted
                    // when old primary's arrays are cleared.
                    var oldAlternateIds = LibraryManager.GetLocalAlternateVersionIds(oldPrimary)
                        .Concat(LibraryManager.GetLinkedAlternateVersions(oldPrimary).Select(v => v.Id))
                        .Distinct()
                        .ToList();

                    foreach (var altId in oldAlternateIds)
                    {
                        if (LibraryManager.GetItemById(altId) is Video altVideo && !altVideo.Id.Equals(newPrimary.Id))
                        {
                            altVideo.SetPrimaryVersionId(newPrimary.Id);
                            altVideo.OwnerId = newPrimary.Id;
                            await altVideo.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    // Then: demote old primary 鈥?clear its arrays and set it as alternate of new primary
                    oldPrimary.LocalAlternateVersions = [];
                    oldPrimary.LinkedAlternateVersions = [];
                    oldPrimary.SetPrimaryVersionId(newPrimary.Id);
                    oldPrimary.OwnerId = newPrimary.Id;
                    await oldPrimary.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);

                    // Re-route playlist/collection references from old primary to new primary
                    await LibraryManager.RerouteLinkedChildReferencesAsync(oldPrimary.Id, newPrimary.Id).ConfigureAwait(false);
                }

                // After removing items, reattach any detached user data to remaining children
                // that share the same user data keys (eg. same episode replaced with a new file).
                if (actuallyRemoved.Count > 0)
                {
                    var removedKeys = actuallyRemoved.SelectMany(i => i.GetUserDataKeys()).ToHashSet();
                    foreach (var child in validChildren)
                    {
                        if (child.GetUserDataKeys().Any(removedKeys.Contains))
                        {
                            await child.ReattachUserDataAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            else
            {
